#include "pch.h"

#include "Matcher.h"
#include "Match.h"


using namespace System::Diagnostics;

using namespace msclr::interop;


namespace Pcre2RegexInterop
{

	static Matcher::Matcher( )
	{
		EmptyEnumeration = gcnew List<IMatch^>( 0 );

		CompileOptions0 = GetCompileOptions0( );
		MatchOptions0 = GetMatchOptions0( );
	}


	Matcher::Matcher( String^ pattern0, cli::array<String^>^ options )
		: mData( nullptr )
	{
		try
		{
			marshal_context context{};

			const wchar_t* pattern = context.marshal_as<const wchar_t*>( pattern0 );

			int compiler_options = 0;

			for each( OptionInfo ^ o in CompileOptions0 )
			{
				if( Array::IndexOf( options, "c:" + o->FlagName ) >= 0 )
				{
					compiler_options |= o->Flag;
				}
			}

			int matcher_options = 0;

			for each( OptionInfo ^ o in CompileOptions0 )
			{
				if( Array::IndexOf( options, "m:" + o->FlagName ) >= 0 )
				{
					matcher_options |= o->Flag;
				}
			}

			mData = new MatcherData{};

			int errornumber;
			PCRE2_SIZE erroroffset;

			pcre2_code* re = pcre2_compile(
				reinterpret_cast<PCRE2_SPTR16>( pattern ), /* the pattern */
				PCRE2_ZERO_TERMINATED, /* indicates pattern is zero-terminated */
				compiler_options,      /* options */
				&errornumber,          /* for error number */
				&erroroffset,          /* for error offset */
				NULL );                /* use default compile context */

			if( re == nullptr )
			{
				PCRE2_UCHAR buffer[256];
				pcre2_get_error_message( errornumber, buffer, sizeof( buffer ) );

				String^ message = gcnew String( reinterpret_cast<wchar_t*>( buffer ) );

				throw gcnew Exception( String::Format( "PCRE2 Error at {0}: {1}.", errornumber, message ) );
			}

			mData->mRe = re;
			mData->mMatcherOptions = matcher_options;
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


	Matcher::~Matcher( )
	{
		this->!Matcher( );
	}


	Matcher::!Matcher( )
	{
		delete mData;
		mData = nullptr;
	}


	String^ Matcher::GetPcre2Version( )
	{
		return String::Format( "{0}.{1}", PCRE2_MAJOR, PCRE2_MINOR );
	}


#define C(f, n) \
	list->Add(gcnew OptionInfo( f, gcnew String(#f), gcnew String(n)));


	List<OptionInfo^>^ Matcher::GetCompileOptions( )
	{
		return CompileOptions0;
	}


	List<OptionInfo^>^ Matcher::GetCompileOptions0( )
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


	List<OptionInfo^>^ Matcher::GetMatchOptions( )
	{
		return MatchOptions0;
	}


	List<OptionInfo^>^ Matcher::GetMatchOptions0( )
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


	RegexMatches^ Matcher::Matches( String^ text0 )
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
				reinterpret_cast<PCRE2_SPTR16>( mData->mText.c_str( ) ),  /* the subject string */
				mData->mText.length( ),  /* the length of the subject */
				0,                       /* start at offset 0 in the subject */
				mData->mMatcherOptions,  /* options */
				mData->mMatchData,       /* block for storing the result */
				NULL );                  /* use default match context */


			if( rc < 0 )
			{
				switch( rc )
				{
				case PCRE2_ERROR_NOMATCH:
					// no matches
					return gcnew RegexMatches( 0, EmptyEnumeration );
				default:
					// other errors
					throw gcnew Exception( String::Format( "PCRE2 Error: {0}", rc ) );
					break;
				}
			}

			if( rc == 0 )
			{
				throw gcnew Exception( "PCRE2 Error: ovector was not big enough for all the captured substrings" );
			}

			PCRE2_SIZE* ovector = pcre2_get_ovector_pointer( mData->mMatchData );

			Match^ match = gcnew Match( this, mData->mRe, ovector, rc );
			matches->Add( match );


			// find next matches

			{
				const auto& re = mData->mRe;
				const auto& match_data = mData->mMatchData;
				const wchar_t* subject = mData->mText.c_str( );
				auto subject_length = mData->mText.length( );


				// their tricky stuffs; code and comments are from 'pcre2demo.c'

				uint32_t option_bits;
				uint32_t newline;
				int crlf_is_newline;
				int utf8;

				/* Before running the loop, check for UTF-8 and whether CRLF is a valid newline
				sequence. First, find the options with which the regex was compiled and extract
				the UTF state. */

				(void)pcre2_pattern_info( re, PCRE2_INFO_ALLOPTIONS, &option_bits );
				utf8 = ( option_bits & PCRE2_UTF ) != 0;

				/* Now find the newline convention and see whether CRLF is a valid newline
				sequence. */

				(void)pcre2_pattern_info( re, PCRE2_INFO_NEWLINE, &newline );
				crlf_is_newline = newline == PCRE2_NEWLINE_ANY ||
					newline == PCRE2_NEWLINE_CRLF ||
					newline == PCRE2_NEWLINE_ANYCRLF;

				/* Loop for second and subsequent matches */

				for( ;;)
				{
					uint32_t options = 0;                   /* Normally no options */
					PCRE2_SIZE start_offset = ovector[1];   /* Start at end of previous match */

					/* If the previous match was for an empty string, we are finished if we are
					at the end of the subject. Otherwise, arrange to run another match at the
					same point to see if a non-empty match can be found. */

					if( ovector[0] == ovector[1] )
					{
						if( ovector[0] == subject_length ) break;
						options = PCRE2_NOTEMPTY_ATSTART | PCRE2_ANCHORED;
					}

					/* If the previous match was not an empty string, there is one tricky case to
					consider. If a pattern contains \K within a lookbehind assertion at the
					start, the end of the matched string can be at the offset where the match
					started. Without special action, this leads to a loop that keeps on matching
					the same substring. We must detect this case and arrange to move the start on
					by one character. The pcre2_get_startchar() function returns the starting
					offset that was passed to pcre2_match(). */

					else
					{
						PCRE2_SIZE startchar = pcre2_get_startchar( match_data );
						if( start_offset <= startchar )
						{
							if( startchar >= subject_length ) break;   /* Reached end of subject.   */
							start_offset = startchar + 1;             /* Advance by one character. */
							if( utf8 )                                 /* If UTF-8, it may be more  */
							{                                       /*   than one code unit.     */
								for( ; start_offset < subject_length; start_offset++ )
									if( ( subject[start_offset] & 0xc0 ) != 0x80 ) break;
							}
						}
					}

					/* Run the next matching operation */

					rc = pcre2_match(
						re,                   /* the compiled pattern */
						reinterpret_cast<PCRE2_SPTR16>( subject ),              /* the subject string */
						subject_length,       /* the length of the subject */
						start_offset,         /* starting offset in the subject */
						options,              /* options */
						match_data,           /* block for storing the result */
						NULL );                /* use default match context */

					  /* This time, a result of NOMATCH isn't an error. If the value in "options"
					  is zero, it just means we have found all possible matches, so the loop ends.
					  Otherwise, it means we have failed to find a non-empty-string match at a
					  point where there was a previous empty-string match. In this case, we do what
					  Perl does: advance the matching position by one character, and continue. We
					  do this by setting the "end of previous match" offset, because that is picked
					  up at the top of the loop as the point at which to start again.

					  There are two complications: (a) When CRLF is a valid newline sequence, and
					  the current position is just before it, advance by an extra byte. (b)
					  Otherwise we must ensure that we skip an entire UTF character if we are in
					  UTF mode. */

					if( rc == PCRE2_ERROR_NOMATCH )
					{
						if( options == 0 ) break;                    /* All matches found */
						ovector[1] = start_offset + 1;              /* Advance one code unit */
						if( crlf_is_newline &&                      /* If CRLF is a newline & */
							start_offset < subject_length - 1 &&    /* we are at CRLF, */
							subject[start_offset] == '\r' &&
							subject[start_offset + 1] == '\n' )
							ovector[1] += 1;                          /* Advance by one more. */
						else if( utf8 )                              /* Otherwise, ensure we */
						{                                         /* advance a whole UTF-8 */
							while( ovector[1] < subject_length )       /* character. */
							{
								if( ( subject[ovector[1]] & 0xc0 ) != 0x80 ) break;
								ovector[1] += 1;
							}
						}
						continue;    /* Go round the loop again */
					}

					/* Other matching errors are not recoverable. */

					if( rc < 0 )
					{
						throw gcnew Exception( String::Format( "PCRE2 Error: Matching error '{0}'", rc ) );
					}

					/* Match succeded */


					/* The match succeeded, but the output vector wasn't big enough. This
					should not happen. */

					if( rc == 0 )
					{
						throw gcnew Exception( "PCRE2 Error: ovector was not big enough for all the captured substrings" );
					}

					Match^ match = gcnew Match( this, mData->mRe, ovector, rc );
					matches->Add( match );

				}      /* End of loop to find second and subsequent matches */
			}


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
