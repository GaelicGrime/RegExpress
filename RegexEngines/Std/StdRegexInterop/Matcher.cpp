#include "pch.h"

#include "Matcher.h"


using namespace System::Diagnostics;

using namespace std;
using namespace msclr::interop;


namespace StdRegexInterop
{

	Matcher::Matcher( String^ pattern0, cli::array<String^>^ options )
		: mData( nullptr )
	{
		try
		{
			marshal_context context{};

			wregex::flag_type regex_flags{};
			regex_constants::match_flag_type match_flags = regex_constants::match_flag_type::match_default;

			wstring pattern = context.marshal_as<wstring>( pattern0 );

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

			}

			mData = new MatcherData{};
			mData->mMatchFlags = match_flags;

			mData->mRegex.assign( std::move( pattern ), regex_flags );
		}
		catch( const regex_error & exc )
		{
			//regex_constants::error_type code = exc.code( );
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( what );
		}
		catch( const exception & exc )
		{
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( "Error: " + what );
		}
		catch( Exception ^ exc )
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



	RegexMatches^ Matcher::Matches( String^ text0 )
	{
		try
		{
			marshal_context context{};

			mData->mText = context.marshal_as<wstring>( text0 );

			auto matches = gcnew List<IMatch^>( );

			auto* native_text = mData->mText.c_str( );

			wcregex_iterator results_begin( native_text, native_text + mData->mText.length( ), mData->mRegex, mData->mMatchFlags );
			wcregex_iterator results_end{};

			for( auto i = results_begin; i != results_end; ++i )
			{
				const wcmatch& match = *i;

				auto m = SimpleMatch::Create( CheckedCast::ToInt32( match.position( ) ), CheckedCast::ToInt32( match.length( ) ), this );
				int j = 0;

				for( auto i = match.cbegin( ); i != match.cend( ); ++i, ++j )
				{
					const std::wcsub_match& submatch = *i;

					String^ group_name = j.ToString( System::Globalization::CultureInfo::InvariantCulture );

					if( !submatch.matched )
					{
						m->AddGroup( 0, 0, false, group_name );
					}
					else
					{
						auto pos = match.position( j );
						int submatch_index = CheckedCast::ToInt32( pos );

						m->AddGroup( submatch_index, CheckedCast::ToInt32( submatch.length( ) ), true, group_name );
					}
				}

				matches->Add( m );
			}

			return gcnew RegexMatches( matches->Count, matches );
		}
		catch( const regex_error & exc )
		{
			//regex_constants::error_type code = exc.code( );
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( what );
		}
		catch( const exception & exc )
		{
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( "Error: " + what );
		}
		catch( Exception ^ exc )
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
