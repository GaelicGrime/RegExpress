#include "pch.h"

#include "Matcher.h"
#include "Group.h"
#include "Match.h"


using namespace System::Diagnostics;

using namespace msclr::interop;


namespace Re2RegexInterop
{

	static Matcher::Matcher( )
	{
		mEmptyEnumeration = gcnew List<IMatch^>( 0 );

		BuildOptions( );
	}


	static std::vector<char> ToUtf8( String^ s )
	{
		const char* old_locale = setlocale( LC_ALL, NULL );
		const char* new_locale = setlocale( LC_CTYPE, ".utf8" );

		if( new_locale == nullptr )
		{
			throw gcnew Exception( "Failed to set locale." );
		}

		std::vector<char> utf8( s->Length );

		pin_ptr<const wchar_t> pinned = PtrToStringChars( s );
		const wchar_t* start = pinned;

		auto mb_cur_max = MB_CUR_MAX;
		mbstate_t mbstate = { 0 };

		size_t bytes_written = 0;

		for( const wchar_t* p = start; *p; ++p ) // (we assume that the array is zero-terminated)
		{
			size_t size_converted;
			errno_t error;

			utf8.resize( bytes_written + mb_cur_max );

			error = wcrtomb_s( &size_converted, utf8.data( ) + bytes_written, mb_cur_max, *p, &mbstate );

			if( error )
			{
				setlocale( LC_ALL, old_locale ); // restore

				String^ err = gcnew String( strerror( error ) );

				throw gcnew Exception( String::Format( "Failed to convert to UTF-8: '{0}'. Source index: {1}.", err, p - start ) );
			}

			bytes_written += size_converted;
		}

		utf8.resize( bytes_written ); // (not zero-terminated)

		setlocale( LC_ALL, old_locale ); // restore

		return utf8;
	}


	void ToUtf8( std::vector<char>* dest, std::vector<int>* indices, String^ s )
	{
		const char* old_locale = setlocale( LC_ALL, NULL );
		const char* new_locale = setlocale( LC_CTYPE, ".utf8" );

		if( new_locale == nullptr )
		{
			throw gcnew Exception( "Failed to set locale." );
		}


		dest->reserve( s->Length );
		indices->reserve( s->Length );

		pin_ptr<const wchar_t> pinned = PtrToStringChars( s );
		const wchar_t* start = pinned;

		auto mb_cur_max = MB_CUR_MAX;
		mbstate_t mbstate = { 0 };

		size_t bytes_written = 0;

		for( const wchar_t* p = start; *p; ++p ) // (we assume that the array is zero-terminated)
		{
			size_t size_converted;
			errno_t error;

			indices->resize( bytes_written + 1, -1 ); // ('-1' will denote unset elements)
			( *indices )[bytes_written] = p - start;

			dest->resize( bytes_written + mb_cur_max );

			error = wcrtomb_s( &size_converted, dest->data( ) + bytes_written, mb_cur_max, *p, &mbstate );

			if( error )
			{
				setlocale( LC_ALL, old_locale ); // restore

				String^ err = gcnew String( strerror( error ) );

				throw gcnew Exception( String::Format( "Failed to convert to UTF-8: '{0}'. Source index: {1}.", err, p - start ) );
			}

			bytes_written += size_converted;
		}

		dest->resize( bytes_written ); // (not zero-terminated)
		indices->resize( bytes_written );

		setlocale( LC_ALL, old_locale ); // restore
	}


	Matcher::Matcher( String^ pattern0, cli::array<String^>^ options )
		: mData( nullptr )
	{
		try
		{
			RE2::Options options{}; // TODO: implement

			std::vector<char> utf8 = ToUtf8( pattern0 );

			re2::StringPiece pattern( utf8.data( ), utf8.size( ) );

			std::unique_ptr<RE2> re( new RE2( pattern, options ) );

			if( !re->ok( ) )
			{
				throw gcnew Exception( String::Format( L"RE2 Error {0}: {1}", (int)re->error_code( ), gcnew String( re->error( ).c_str( ) ) ) );
			}

			re.reset( );
			utf8.insert( utf8.begin( ), '(' );
			utf8.push_back( ')' );

			pattern.set( utf8.data( ), utf8.size( ) );
			re.reset( new RE2( pattern, options ) );

			if( !re->ok( ) )
			{
				throw gcnew Exception( String::Format( L"RE2 Error 2/{0}: {1}", (int)re->error_code( ), gcnew String( re->error( ).c_str( ) ) ) );
			}


			mData = new MatcherData{};

			mData->mRe = std::move( re );
		}
		catch( const std::exception & exc )
		{
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( "Error: " + what );
		}
		catch( Exception ^ exc )
		{
			throw exc;
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


	String^ Matcher::GetRe2Version( )
	{
		return L"2020-01-01";
	}


	RegexMatches^ Matcher::Matches( String^ text0 )
	{
		// TODO: re-implement as lazy enumerator?

		try
		{
			OriginalText = text0;

			auto matches = gcnew List<IMatch^>( );

			ToUtf8( &mData->mText, &mData->mIndices, text0 );
			re2::StringPiece text( mData->mText.data( ), mData->mText.size( ) );

			int number_of_capturing_groups = mData->mRe->NumberOfCapturingGroups( );

			mData->mDefinedGroups.resize( number_of_capturing_groups );

			std::vector<RE2::Arg> args;
			for( int i = 0; i < mData->mDefinedGroups.size( ); ++i ) args.push_back( &mData->mDefinedGroups[i] );

			std::vector<const RE2::Arg*> argsp;
			for( int i = 0; i < args.size( ); ++i ) argsp.push_back( &args[i] );

			//....................
			// EMPTY MATCHES

			auto prev_data = text.data( );

			while( RE2::FindAndConsumeN( &text, *mData->mRe, argsp.data( ), number_of_capturing_groups ) )
			{
				Match^ match = nullptr;
				for( int i = 0; i < mData->mDefinedGroups.size( ); ++i )
				{
					// TODO: use group names

					const auto& g = mData->mDefinedGroups[i];
					if( g.data( ) == nullptr )
					{
						match->AddGroup( gcnew Group( match,
							i.ToString( System::Globalization::CultureInfo::InvariantCulture ),
							-1, 0 ) );
					}
					else
					{
						int utf8index = g.data( ) - mData->mText.data( );
						int index = mData->mIndices.at( utf8index );
						if( index < 0 )
						{
							throw gcnew Exception( "Index error." );
						}

						if( i == 0 ) match = gcnew Match( this, index, g.size( ) );

						match->AddGroup( gcnew Group( match,
							i.ToString( System::Globalization::CultureInfo::InvariantCulture ),
							index, g.size( ) ) ); // main group when 'i==0'
					}
				}

				matches->Add( match );

				if( prev_data == text.data( ) )
				{
					// it was an empty match;
					// advance?
					//...

					text.remove_prefix( 1 ); //.................
				}

				prev_data = text.data( );
			}

			return gcnew RegexMatches( matches->Count, matches );
		}
		catch( const std::exception & exc )
		{
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( "Error: " + what );
		}
		catch( Exception ^ exc )
		{
			throw exc;
		}
		catch( ... )
		{
			// TODO: also catch 'boost::exception'?
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}


	void Matcher::BuildOptions( )
	{
#define C(f, n) \
	list->Add(gcnew OptionInfo( f, gcnew String(#f), gcnew String(n)));

		List<OptionInfo^>^ list = gcnew List<OptionInfo^>( );

		//C( PCRE2_ANCHORED, "Force pattern anchoring" );

		//mCompileOptions = list;


#undef C
	}
}
