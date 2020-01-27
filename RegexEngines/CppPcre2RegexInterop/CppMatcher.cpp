#include "pch.h"

#include "CppMatcher.h"
#include "CppMatch.h"


using namespace System::Diagnostics;

using namespace msclr::interop;


namespace CppPcre2RegexInterop
{

	CppMatcher::CppMatcher( String^ pattern0, cli::array<String^>^ options )
		: mData( nullptr )
	{
		try
		{
			marshal_context context{};

			std::wstring pattern = context.marshal_as<std::wstring>( pattern0 );

			int regex_flags = 0;

			for each( String ^ o in options )
			{
#define C(n) \
	if( o == L#n ) regex_flags |= n; \
	else

				C( PCRE2_ALLOW_EMPTY_CLASS )
					//.............
					//C( ECMAScript )
					//C( JavaScript )
					//C( JScript )
					//C( perl )
					//C( basic )
					//C( sed )
					//C( extended )
					//C( awk )
					//C( grep )
					//C( egrep )
					//C( emacs )
					//C( literal )

					//C( icase )
					//C( nosubs )
					//C( optimize )
					//C( collate )
					////?C( newline_alt ) // wrong documentation?
					//C( no_except )
					//C( save_subexpression_location )
					//C( no_mod_m )
					//C( no_mod_s )
					//C( mod_s )
					//C( mod_x )
					//C( no_empty_expressions )
					//C( no_escape_in_lists )
					////?C( no_bk_refs ) // wrong documentation?
					//C( no_char_classes )
					//C( no_intervals )
					//C( bk_plus_qm )
					//C( bk_vbar )

					;

#undef C

//#define C(n) \
//	if( o == L#n ) match_flags |= regex_constants::match_flag_type::##n; \
//	else
//
//				C( match_default )
//					C( match_not_bob )
//					C( match_not_eob )
//					C( match_not_bol )
//					C( match_not_eol )
//					C( match_not_bow )
//					C( match_not_eow )
//					C( match_any )
//					C( match_not_null )
//					C( match_continuous )
//					C( match_partial )
//					C( match_single_line )
//					C( match_prev_avail )
//					C( match_not_dot_newline )
//					C( match_not_dot_null )
//					C( match_posix )
//					C( match_perl )
//					C( match_nosubs )
//					C( match_extra )
//					;
//#undef C
			}

			mData = new MatcherData{};

			int errornumber;
			PCRE2_SIZE erroroffset;

			pcre2_code * re = pcre2_compile(
				reinterpret_cast<PCRE2_SPTR16>(pattern.c_str()),       /* the pattern */
				PCRE2_ZERO_TERMINATED, /* indicates pattern is zero-terminated */
				0,                     /* default options */
				&errornumber,          /* for error number */
				&erroroffset,          /* for error offset */
				NULL );                 /* use default compile context */

			if( re == nullptr )
			{
				PCRE2_UCHAR buffer[256];
				pcre2_get_error_message( errornumber, buffer, sizeof( buffer ) );

				String^ message = gcnew String( reinterpret_cast<wchar_t*>( buffer) );

				throw gcnew Exception( String::Format( "PCRE Error at {0}: {1}.", errornumber, message ));
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


	RegexMatches^ CppMatcher::Matches( String^ text0 )
	{
		// TODO: re-implement as lazy enumerator?

		try
		{
			auto matches = gcnew List<IMatch^>( );

			marshal_context context{};

			mData->mText = context.marshal_as<std::wstring>( text0 );
			mData->mMatchData = pcre2_match_data_create_from_pattern( mData->mRe, NULL );

			int rc = pcre2_match(
				mData->mRe,           /* the compiled pattern */
				reinterpret_cast<PCRE2_SPTR16>(mData->mText.c_str()),              /* the subject string */
				mData->mText.length(),       /* the length of the subject */
				0,                    /* start at offset 0 in the subject */
				0,                    /* default options */
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
