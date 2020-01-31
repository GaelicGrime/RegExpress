#include "pch.h"

#include "Matcher.h"
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


	struct SpecialConversion
	{
		std::vector<char> mUtf8;
		std::vector<int> mUtf8IndexToOriginal;

		void ConvertFrom( String^ s )
		{
			const char* old_locale = setlocale( LC_ALL, NULL );
			const char* new_locale = setlocale( LC_CTYPE, ".utf8" );

			if( new_locale == nullptr )
			{
				throw gcnew Exception( "Failed to set locale." );
			}


			mUtf8.reserve( s->Length );
			mUtf8IndexToOriginal.reserve( s->Length );

			pin_ptr<const wchar_t> pinned = PtrToStringChars( s );
			const wchar_t* start = pinned;

			auto mb_cur_max = MB_CUR_MAX;
			mbstate_t mbstate = { 0 };

			size_t bytes_written = 0;

			for( const wchar_t* p = start; *p; ++p ) // (we assume that the array is zero-terminated)
			{
				size_t size_converted;
				errno_t error;

				mUtf8IndexToOriginal.resize( bytes_written + 1, -1 ); // ('-1' will denote unset elements)
				mUtf8IndexToOriginal[bytes_written] = p - start;

				mUtf8.resize( bytes_written + mb_cur_max );

				error = wcrtomb_s( &size_converted, mUtf8.data( ) + bytes_written, mb_cur_max, *p, &mbstate );

				if( error )
				{
					setlocale( LC_ALL, old_locale );

					String^ err = gcnew String( strerror( error ) );

					throw gcnew Exception( String::Format( "Failed to convert to UTF-8: '{0}'. Source index: {1}.", err, p - start ) );
				}

				bytes_written += size_converted;

				++p;
			}

			mUtf8.resize( bytes_written );

			setlocale( LC_ALL, old_locale ); // restore
		}
	};


	Matcher::Matcher( String^ pattern0, cli::array<String^>^ options )
		: mData( nullptr )
	{
		try
		{
			SpecialConversion sc;
			sc.ConvertFrom( pattern0 );

			RE2::Options options{}; // TODO: implement



			re2::StringPiece utf8_pattern( sc.mUtf8.data(), sc.mUtf8.size());





			//....................

#if 0
			marshal_context context{};

			cli::array<Byte>^ utf8_bytes = System::Text::Encoding::UTF8->GetBytes( pattern0 );
			pin_ptr<Byte> utf8_pinned = &utf8_bytes[0];
			const unsigned char* utf8_uchars = utf8_pinned;
			const char* utf8_chars = reinterpret_cast<const char*>( utf8_uchars );

			//std::string utf8_string( utf8_chars, utf8_bytes->Length );

			//const wchar_t* pattern = context.marshal_as<const wchar_t*>( pattern0 );

			RE2::Options options{}; // TODO: implement

			re2::StringPiece utf8_pattern( utf8_chars, utf8_bytes->Length );


			RE2 re( utf8_pattern, options );

			if( re.error_code( ) != RE2::NoError )
			{
				throw gcnew Exception( String::Format( L"RE2 Error {0}: {1}", (int)re.error_code( ), gcnew String( re.error( ).c_str( ) ) ) );
			}


			int number_of_groups = re.NumberOfCapturingGroups( );

			//std::string 


			mData = new MatcherData{};

#endif

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
			auto matches = gcnew List<IMatch^>( );

			marshal_context context{};

			mData->mText = context.marshal_as<std::wstring>( text0 );

			//.......

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
