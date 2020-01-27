#include "pch.h"

#include "CppMatcher.h"
#include "CppMatch.h"


using namespace System::Diagnostics;

using namespace msclr::interop;


namespace CppPcre2RegexInterop
{

	static CppMatcher::CppMatcher( )
	{
		CompileOptions0 = GetCompileOptions0( );
		MatchOptions0 = GetMatchOptions0( );
	}


	CppMatcher::CppMatcher( String^ pattern0, cli::array<String^>^ options )
		: mData( nullptr )
	{
		try
		{
			marshal_context context{};

			std::wstring pattern = context.marshal_as<std::wstring>( pattern0 );

			int regex_flags = 0;

			for each( OptionInfo ^ o in CompileOptions0 )
			{
				if( Array::IndexOf( options, "c:" + o->FlagName ) >= 0 )
				{
					regex_flags |= o->Flag;
				}
			}

			mData = new MatcherData{};

			int errornumber;
			PCRE2_SIZE erroroffset;

			pcre2_code* re = pcre2_compile(
				reinterpret_cast<PCRE2_SPTR16>( pattern.c_str( ) ),       /* the pattern */
				PCRE2_ZERO_TERMINATED, /* indicates pattern is zero-terminated */
				0,                     /* default options */
				&errornumber,          /* for error number */
				&erroroffset,          /* for error offset */
				NULL );                 /* use default compile context */

			if( re == nullptr )
			{
				PCRE2_UCHAR buffer[256];
				pcre2_get_error_message( errornumber, buffer, sizeof( buffer ) );

				String^ message = gcnew String( reinterpret_cast<wchar_t*>( buffer ) );

				throw gcnew Exception( String::Format( "PCRE Error at {0}: {1}.", errornumber, message ) );
			}

			mData->mRe = re;
		}
		catch( const std::exception & exc )
		{
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( "Error: " + what );
		}
		catch( ... )
		{
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}


	CppMatcher::~CppMatcher( )
	{
		this->!CppMatcher( );
	}


	CppMatcher::!CppMatcher( )
	{
		delete mData;
		mData = nullptr;
	}


	String^ CppMatcher::GetPcre2Version( )
	{
		return String::Format( "{0}.{1}", PCRE2_MAJOR, PCRE2_MINOR );
	}


#define C(f, n) \
	list->Add(gcnew OptionInfo( f, gcnew String(#f), gcnew String(n)));


	List<OptionInfo^>^ CppMatcher::GetCompileOptions( )
	{
		return CompileOptions0;
	}


	List<OptionInfo^>^ CppMatcher::GetCompileOptions0( )
	{
		List<OptionInfo^>^ list = gcnew List<OptionInfo^>( );

		C( PCRE2_ANCHORED, "Force pattern anchoring" );
		C( PCRE2_ALLOW_EMPTY_CLASS, "Allow empty classes" );
		C( PCRE2_ALT_BSUX, "Alternative handling of \\u, \\U, and \\x" );
		C( PCRE2_ALT_CIRCUMFLEX, "Alternative handling of ^ in multiline mode" );
		C( PCRE2_ALT_VERBNAMES, "Process backslashes in verb names" );
		C( PCRE2_AUTO_CALLOUT, "Compile automatic callouts" );
		C( PCRE2_CASELESS, "Do caseless matching" );
		C( PCRE2_DOLLAR_ENDONLY, "$ not to match newline at end" );
		C( PCRE2_DOTALL, ". matches anything including NL" );
		C( PCRE2_DUPNAMES, "Allow duplicate names for subpatterns" );
		C( PCRE2_ENDANCHORED, "Pattern can match only at end of subject" );
		C( PCRE2_EXTENDED, "Ignore white space and # comments" );
		C( PCRE2_FIRSTLINE, "Force matching to be before newline" );
		C( PCRE2_LITERAL, "Pattern characters are all literal" );
		C( PCRE2_MATCH_INVALID_UTF, "Enable support for matching invalid UTF" );
		C( PCRE2_MATCH_UNSET_BACKREF, "Match unset backreferences" );
		C( PCRE2_MULTILINE, "^ and $ match newlines within data" );
		C( PCRE2_NEVER_BACKSLASH_C, "Lock out the use of \\C in patterns" );
		C( PCRE2_NEVER_UCP, "Lock out PCRE2_UCP, e.g. via (*UCP)" );
		C( PCRE2_NEVER_UTF, "Lock out PCRE2_UTF, e.g. via (*UTF)" );
		C( PCRE2_NO_AUTO_CAPTURE, "Disable numbered capturing parentheses (named ones available)" );
		C( PCRE2_NO_AUTO_POSSESS, "Disable auto-possessification" );
		C( PCRE2_NO_DOTSTAR_ANCHOR, "Disable automatic anchoring for .*" );
		C( PCRE2_NO_START_OPTIMIZE, "Disable match-time start optimizations" );
		C( PCRE2_NO_UTF_CHECK, "Do not check the pattern for UTF validity (only relevant if PCRE2_UTF is set)" );
		C( PCRE2_UCP, "Use Unicode properties for \\d, \\w, etc." );
		C( PCRE2_UNGREEDY, "Invert greediness of quantifiers" );
		C( PCRE2_USE_OFFSET_LIMIT, "Enable offset limit for unanchored matching" );
		C( PCRE2_UTF, "Treat pattern and subjects as UTF strings" );

		// TODO: define extra-options too

		return list;
	}


	List<OptionInfo^>^ CppMatcher::GetMatchOptions( )
	{
		return MatchOptions0;
	}


	List<OptionInfo^>^ CppMatcher::GetMatchOptions0( )
	{
		List<OptionInfo^>^ list = gcnew List<OptionInfo^>( );

		C( PCRE2_ANCHORED, "Match only at the first position" );
		C( PCRE2_COPY_MATCHED_SUBJECT, "On success, make a private subject copy" );
		C( PCRE2_ENDANCHORED, "Pattern can match only at end of subject" );
		C( PCRE2_NOTBOL, "Subject string is not the beginning of a line" );
		C( PCRE2_NOTEOL, "Subject string is not the end of a line" );
		C( PCRE2_NOTEMPTY, "An empty string is not a valid match" );
		C( PCRE2_NOTEMPTY_ATSTART, "An empty string at the start of the subject is not a valid match" );
		C( PCRE2_NO_JIT, "Do not use JIT matching" );
		C( PCRE2_NO_UTF_CHECK, "Do not check the subject for UTF validity (only relevant if PCRE2_UTF was set at compile time)" );
		C( PCRE2_PARTIAL_HARD, "Return PCRE2_ERROR_PARTIAL for a partial match even if there is a full match" );
		C( PCRE2_PARTIAL_SOFT, "Return PCRE2_ERROR_PARTIAL for a partial match if no full matches are found" );

		return list;
	}

#undef C


	RegexMatches^ CppMatcher::Matches( String^ text0 )
	{
		// TODO: re-implement as lazy enumerator?

		try
		{
			auto matches = gcnew List<IMatch^>( );

			marshal_context context{};

			mData->mText = context.marshal_as<std::wstring>( text0 );
			mData->mMatchData = pcre2_match_data_create_from_pattern( mData->mRe, NULL );


			int options = 0; //............


			int rc = pcre2_match(
				mData->mRe,           /* the compiled pattern */
				reinterpret_cast<PCRE2_SPTR16>( mData->mText.c_str( ) ),              /* the subject string */
				mData->mText.length( ),       /* the length of the subject */
				0,                    /* start at offset 0 in the subject */
				options,                    /* default options */
				mData->mMatchData,    /* block for storing the result */
				NULL );                /* use default match context */


			if( rc < 0 )
			{
				switch( rc )
				{
				case PCRE2_ERROR_NOMATCH:
					// no matches
					return gcnew RegexMatches( 0, nullptr );
					return nullptr;
				default:
					// other errors
					throw gcnew Exception( String::Format( "PCRE Error: {0}", rc ) );
					break;
				}
			}

			PCRE2_SIZE* ovector;

			//............



			return gcnew RegexMatches( matches->Count, matches );
		}
		catch( const std::exception & exc )
		{
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( "Error: " + what );
		}
		catch( ... )
		{
			// TODO: also catch 'boost::exception'?
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}

}
