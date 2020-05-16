#include "pch.h"

#include "NativeMatcher.h"
#include "Matcher.h"


using namespace System::Diagnostics;
using namespace System::Globalization;

using namespace std;
using namespace msclr::interop;


namespace StdRegexInterop
{

	long Matcher::Default_REGEX_MAX_STACK_COUNT::get( ) { return StdRegexInterop::Default_REGEX_MAX_STACK_COUNT; }
	long Matcher::Default_REGEX_MAX_COMPLEXITY_COUNT::get( ) { return StdRegexInterop::Default_REGEX_MAX_COMPLEXITY_COUNT; }


	static Matcher::Matcher( )
	{
		ConstOptionPrefix_REGEX_MAX_STACK_COUNT = "REGEX_MAX_STACK_COUNT:";
		ConstOptionPrefix_REGEX_MAX_COMPLEXITY_COUNT = "REGEX_MAX_COMPLEXITY_COUNT:";
	}


	Matcher::Matcher( String^ pattern0, cli::array<String^>^ options )
		: mData( nullptr )
	{
		try
		{
			marshal_context context{};

			wregex::flag_type regex_flags{};
			regex_constants::match_flag_type match_flags = regex_constants::match_flag_type::match_default;

			wstring pattern = context.marshal_as<wstring>( pattern0 );

			long lREGEX_MAX_STACK_COUNT = 600L;
			long lREGEX_MAX_COMPLEXITY_COUNT = 10000000L;

			for each( String ^ o in options )
			{
#define C(n) \
	if( o == L#n ) regex_flags |= regex_constants::syntax_option_type::##n; \
	else

				C( ECMAScript )
					C( basic )
					C( extended )
					C( awk )
					C( grep )
					C( egrep )
					C( icase )
					C( nosubs )
					C( optimize )
					C( collate )
					;

#undef C

#define C(n) \
	if( o == L#n ) match_flags |= regex_constants::match_flag_type::##n; \
	else

				C( match_not_bol )
					C( match_not_eol )
					C( match_not_bow )
					C( match_not_eow )
					C( match_any )
					C( match_not_null )
					C( match_continuous )
					C( match_prev_avail )
					;

#undef C

				if( o->StartsWith( ConstOptionPrefix_REGEX_MAX_STACK_COUNT ) )
				{
					String^ s = o->Substring( ConstOptionPrefix_REGEX_MAX_STACK_COUNT->Length );
					if( !String::IsNullOrWhiteSpace( s ) )
					{
						int v;
						if( long::TryParse( s,
							NumberStyles::AllowLeadingSign | NumberStyles::AllowLeadingWhite | NumberStyles::AllowTrailingWhite | NumberStyles::AllowThousands,
							CultureInfo::InvariantCulture,
							v ) )
						{
							lREGEX_MAX_STACK_COUNT = v;
						}
						else
						{
							throw gcnew Exception( String::Format( CultureInfo::InvariantCulture, "Invalid option: ‘REGEX_MAX_STACK_COUNT’. Please enter an integer number, or set to 0 to disable the limit. The default value is {0:#,##0}.", Default_REGEX_MAX_STACK_COUNT ) );
						}
					}
				}

				if( o->StartsWith( ConstOptionPrefix_REGEX_MAX_COMPLEXITY_COUNT ) )
				{
					String^ s = o->Substring( ConstOptionPrefix_REGEX_MAX_COMPLEXITY_COUNT->Length );
					if( !String::IsNullOrWhiteSpace( s ) )
					{
						int v;
						if( long::TryParse( s,
							NumberStyles::AllowLeadingSign | NumberStyles::AllowLeadingWhite | NumberStyles::AllowTrailingWhite | NumberStyles::AllowThousands,
							CultureInfo::InvariantCulture,
							v ) )
						{
							lREGEX_MAX_COMPLEXITY_COUNT = v;
						}
						else
						{
							throw gcnew Exception( String::Format( CultureInfo::InvariantCulture, "Invalid option: ‘REGEX_MAX_COMPLEXITY_COUNT’. Please enter an integer number, or set to 0 to disable the limit. The default value is {0:#,##0}.", Default_REGEX_MAX_COMPLEXITY_COUNT ) );
						}
					}
				}

			}

			mData = new MatcherData{};
			mData->mMatchFlags = match_flags;
			mData->mREGEX_MAX_STACK_COUNT = lREGEX_MAX_STACK_COUNT;
			mData->mREGEX_MAX_COMPLEXITY_COUNT = lREGEX_MAX_COMPLEXITY_COUNT;
			mData->mRegex.assign( std::move( pattern ), regex_flags );
		}
		catch( const regex_error& exc )
		{
			//regex_constants::error_type code = exc.code( );
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( what );
		}
		catch( const exception& exc )
		{
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( "Error: " + what );
		}
		catch( Exception^ exc )
		{
			UNREFERENCED_PARAMETER( exc );
			throw;
		}
		catch( ... )
		{
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}


	Matcher::~Matcher( )
	{
		this->!Matcher( );
	}


	Matcher::!Matcher( )
	{
		delete mData;
		mData = nullptr;
	}


	String^ Matcher::GetCRTVersion( )
	{
		// see "crtversion.h"

		return String::Format( "{0}.{1}.{2}", _VC_CRT_MAJOR_VERSION, _VC_CRT_MINOR_VERSION, _VC_CRT_BUILD_VERSION );
	}


	private ref struct WatchingThreadData
	{
		ICancellable^ cnc;
		Threading::EventWaitHandle^ ev;
	};


	static void WatchCancelRequestThreadProc( Object^ args )
	{
		auto d = (WatchingThreadData^)args;

		try
		{
			for( ;;)
			{
				if( d->cnc->IsCancellationRequested )
				{
					d->ev->Set( );

					break;
				}

				Threading::Thread::Sleep( 222 );
			}
		}
		catch( Threading::ThreadInterruptedException^ )
		{
			// ignore
		}
		catch( Threading::ThreadAbortException^ )
		{
			// ignore
		}
	}


	RegexMatches^ Matcher::Matches( String^ text0, ICancellable^ cnc )
	{
		try
		{
			marshal_context context{};

			mData->mText = context.marshal_as<wstring>( text0 );

			auto matches = gcnew List<IMatch^>( );

			auto* native_text = mData->mText.c_str( );

			std::string native_error_text;

			std::vector<NativeMatch> native_matches;
			native_matches.reserve( 512 );

			auto cancelEvent = gcnew Threading::ManualResetEvent( false );
			auto watchingThread = gcnew Threading::Thread( gcnew Threading::ParameterizedThreadStart( &WatchCancelRequestThreadProc ) );
			auto data = gcnew WatchingThreadData;
			data->cnc = cnc;
			data->ev = cancelEvent;
			watchingThread->IsBackground = true;
			watchingThread->Start( data );

			try
			{
				NativeMatches( &native_matches, &native_error_text, mData->mRegex, native_text, mData->mMatchFlags,
					mData->mREGEX_MAX_STACK_COUNT, mData->mREGEX_MAX_COMPLEXITY_COUNT, (HANDLE)cancelEvent->Handle );
			}
			finally
			{
				try
				{
					watchingThread->Interrupt( );
					watchingThread->Abort( );
				}
				catch( Exception^ )
				{
					Debug::Assert( false );
					// ignore
				}
			}

			if( cnc->IsCancellationRequested )  return RegexMatches::Empty;

			if( !native_error_text.empty( ) )
			{
				throw gcnew Exception( gcnew String( native_error_text.c_str( ) ) );
			}

			SimpleMatch^ match = nullptr;
			int group_index = 0;

			for( const NativeMatch& nm : native_matches )
			{
				if( cnc->IsCancellationRequested )  return RegexMatches::Empty;

				switch( nm.Type )
				{
				case NativeMatch::TypeEnum::M:
					match = SimpleMatch::Create( CheckedCast::ToInt32( nm.Index ), CheckedCast::ToInt32( nm.Length ), this );
					matches->Add( match );
					group_index = 0;
					break;

				case NativeMatch::TypeEnum::G:
					;
					{
						String^ group_name = group_index.ToString( CultureInfo::InvariantCulture );

						if( nm.Index < 0 )
						{
							match->AddGroup( 0, 0, false, group_name );
						}
						else
						{
							match->AddGroup( CheckedCast::ToInt32( nm.Index ), CheckedCast::ToInt32( nm.Length ), true, group_name );
						}

						++group_index;
					}
					break;

				default:
					Debug::Assert( false );
					break;
				}
			}

			return gcnew RegexMatches( matches->Count, matches );
		}
		catch( const regex_error& exc )
		{
			//regex_constants::error_type code = exc.code( );
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( what );
		}
		catch( const exception& exc )
		{
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( "Error: " + what );
		}
		catch( Exception^ exc )
		{
			UNREFERENCED_PARAMETER( exc );
			throw;
		}
		catch( ... )
		{
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}


	String^ Matcher::GetText( int index, int length )
	{
		return gcnew String( mData->mText.c_str( ), index, length );
	}

}
