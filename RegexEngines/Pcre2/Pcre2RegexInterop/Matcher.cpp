#include "pch.h"

#include "Matcher.h"


using namespace System::Diagnostics;

using namespace msclr::interop;


namespace Pcre2RegexInterop
{

	static Matcher::Matcher( )
	{
		mEmptyEnumeration = gcnew List<IMatch^>( 0 );

		BuildOptions( );
	}


	Matcher::Matcher( String^ pattern0, cli::array<String^>^ options )
		: mData( nullptr )
	{
		try
		{
			marshal_context context{};

			const wchar_t* pattern = context.marshal_as<const wchar_t*>( pattern0 );

			int compile_options = 0;

			for each( OptionInfo ^ o in mCompileOptions )
			{
				if( Array::IndexOf( options, "c:" + o->FlagName ) >= 0 )
				{
					compile_options |= o->Flag;
				}
			}

			int extra_compile_options = 0;

			for each( OptionInfo ^ o in mExtraCompileOptions )
			{
				if( Array::IndexOf( options, "x:" + o->FlagName ) >= 0 )
				{
					extra_compile_options |= o->Flag;
				}
			}

			int matcher_options = 0;

			for each( OptionInfo ^ o in mMatchOptions )
			{
				if( Array::IndexOf( options, "m:" + o->FlagName ) >= 0 )
				{
					matcher_options |= o->Flag;
				}
			}

			mData = new MatcherData{};

			mData->mAlgorithm = Array::IndexOf( options, "DFA" ) >= 0 ? Algorithm::DFA : Algorithm::Standard;
			mData->mMatcherOptions = matcher_options;

			mData->mCompileContext = pcre2_compile_context_create( NULL );
			if( mData->mCompileContext == nullptr )
			{
				throw gcnew Exception( "PCRE2 Error : Failed to create compile context." );
			}

			pcre2_set_compile_extra_options( mData->mCompileContext, extra_compile_options );

			int errornumber;
			PCRE2_SIZE erroroffset;

			pcre2_code* re = pcre2_compile(
				reinterpret_cast<PCRE2_SPTR16>( pattern ), /* the pattern */
				PCRE2_ZERO_TERMINATED, /* indicates pattern is zero-terminated */
				compile_options,       /* options */
				&errornumber,          /* for error number */
				&erroroffset,          /* for error offset */
				mData->mCompileContext ); /* compile context */

			if( re == nullptr )
			{
				PCRE2_UCHAR buffer[256];
				pcre2_get_error_message( errornumber, buffer, _countof( buffer ) );

				String^ message = gcnew String( reinterpret_cast<wchar_t*>( buffer ) );

				throw gcnew Exception( String::Format( "PCRE2 Error {0} at {1}: {2}.", errornumber, erroroffset, message ) );
			}

			mData->mRe = re;
		}
		catch( const std::exception & exc )
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


	String^ Matcher::GetPcre2Version( )
	{
		return String::Format( "{0}.{1}", PCRE2_MAJOR, PCRE2_MINOR );
	}


	RegexMatches^ Matcher::Matches( String^ text0, ICancellable^ cnc )
	{
		try
		{
			auto matches = gcnew List<IMatch^>( );

			marshal_context context{};

			mData->mText = context.marshal_as<std::wstring>( text0 );

			mData->mMatchContext = pcre2_match_context_create( NULL );
			if( mData->mMatchContext == nullptr )
			{
				throw gcnew Exception( "PCRE2 Error : Failed to create match context" );
			}

			int rc;

			switch( mData->mAlgorithm )
			{
			case Algorithm::DFA:
			{
				mData->mDfaWorkspace.resize( 1000 ); // (see 'pcre2test.c')
				mData->mMatchData = pcre2_match_data_create( 1000, NULL );

				rc = pcre2_dfa_match(
					mData->mRe,           /* the compiled pattern */
					reinterpret_cast<PCRE2_SPTR16>( mData->mText.c_str( ) ),  /* the subject string */
					PCRE2_ZERO_TERMINATED,  /* the length of the subject */
					0,                       /* start at offset 0 in the subject */
					mData->mMatcherOptions,  /* options */
					mData->mMatchData,       /* block for storing the result */
					mData->mMatchContext,    /* match context */
					mData->mDfaWorkspace.data( ),
					mData->mDfaWorkspace.size( )
				);
			}
			break;

			case Algorithm::Standard:
			default:
			{
				mData->mMatchData = pcre2_match_data_create_from_pattern( mData->mRe, NULL );
				rc = pcre2_match(
					mData->mRe,           /* the compiled pattern */
					reinterpret_cast<PCRE2_SPTR16>( mData->mText.c_str( ) ),  /* the subject string */
					PCRE2_ZERO_TERMINATED,  /* the length of the subject */
					0,                       /* start at offset 0 in the subject */
					mData->mMatcherOptions,  /* options */
					mData->mMatchData,       /* block for storing the result */
					mData->mMatchContext     /* match context */
				);
			}
			break;
			}

			if( rc < 0 )
			{
				switch( rc )
				{
				case PCRE2_ERROR_NOMATCH:
					// no matches
					return gcnew RegexMatches( 0, mEmptyEnumeration );
				default:
				{
					PCRE2_UCHAR buffer[256];
					pcre2_get_error_message( rc, buffer, _countof( buffer ) );

					String^ message = gcnew String( reinterpret_cast<wchar_t*>( buffer ) );

					throw gcnew Exception( String::Format( "PCRE2 Error {0} : {1}.", rc, message ) );
				}
				}
			}

			if( rc == 0 )
			{
				throw gcnew Exception( "PCRE2 Error: ovector was not big enough for all the captured substrings" );
			}

			PCRE2_SIZE* ovector = pcre2_get_ovector_pointer( mData->mMatchData );

			if( ovector[0] > ovector[1] )
			{
				// TODO: show more details; see 'pcre2demo.c'
				throw gcnew Exception( String::Format( "PCRE2 Error: {0}",
					"\\K was used in an assertion to set the match start after its end." ) );
			}

			auto match = CreateMatch( mData->mRe, ovector, rc );
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
						PCRE2_UCHAR buffer[256];
						pcre2_get_error_message( rc, buffer, _countof( buffer ) );

						String^ message = gcnew String( reinterpret_cast<wchar_t*>( buffer ) );

						throw gcnew Exception( String::Format( "PCRE2 Error {0} : {1}.", rc, message ) );
					}

					/* Match succeded */


					/* The match succeeded, but the output vector wasn't big enough. This
					should not happen. */

					if( rc == 0 )
					{
						throw gcnew Exception( "PCRE2 Error: ovector was not big enough for all the captured substrings" );
					}

					if( ovector[0] > ovector[1] )
					{
						// TODO: show more details; see 'pcre2demo.c'
						throw gcnew Exception( String::Format( "PCRE2 Error: {0}",
							"\\K was used in an assertion to set the match start after its end." ) );
					}

					auto match = CreateMatch( mData->mRe, ovector, rc );
					matches->Add( match );

				} /* End of loop to find second and subsequent matches */
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


	IMatch^ Matcher::CreateMatch( pcre2_code* re, PCRE2_SIZE* ovector, int rc )
	{
		if( ovector[0] > ovector[1] )
		{
			// TODO: show more details; see 'pcre2demo.c'
			throw gcnew Exception( String::Format( "PCRE2 Error: {0}",
				"\\K was used in an assertion to set the match start after its end." ) );
		}

		auto match = SimpleMatch::Create( CheckedCast::ToInt32n( ovector[0] ), CheckedCast::ToInt32( ovector[1] - ovector[0] ), this );

		// add all groups; the names will be put later
		// group [0] is the whole match

		for( int i = 0; i < rc; ++i )
		{
			auto index = CheckedCast::ToInt32n( ovector[2 * i] );
			auto length = CheckedCast::ToInt32( ovector[2 * i + 1] - ovector[2 * i] );

			match->AddGroup( index, length, index >= 0, i.ToString( System::Globalization::CultureInfo::InvariantCulture ) );
		}

		// add failed groups not included in 'rc'
		{
			uint32_t capturecount;

			if( pcre2_pattern_info(
				re,
				PCRE2_INFO_CAPTURECOUNT,
				&capturecount ) == 0 )
			{
				int total_captures = CheckedCast::ToInt32( capturecount );
				for( int i = rc; i <= total_captures; ++i )
				{
					match->AddGroup( -1, 0, false, i.ToString( System::Globalization::CultureInfo::InvariantCulture ) );
				}
			}
		}

		uint32_t namecount;

		(void)pcre2_pattern_info(
			re,                   /* the compiled pattern */
			PCRE2_INFO_NAMECOUNT, /* get the number of named substrings */
			&namecount );         /* where to put the answer */

		if( namecount > 0 )
		{
			PCRE2_SPTR name_table;
			uint32_t name_entry_size;
			PCRE2_SPTR tabptr;

			(void)pcre2_pattern_info(
				re,                       /* the compiled pattern */
				PCRE2_INFO_NAMETABLE,     /* address of the table */
				&name_table );            /* where to put the answer */

			(void)pcre2_pattern_info(
				re,                       /* the compiled pattern */
				PCRE2_INFO_NAMEENTRYSIZE, /* size of each entry in the table */
				&name_entry_size );       /* where to put the answer */

			tabptr = name_table;
			int total_names = CheckedCast::ToInt32( namecount );
			for( int i = 0; i < total_names; i++ )
			{
				int n = *( (__int16*)tabptr );

				String^ name = gcnew String( reinterpret_cast<const wchar_t*>( ( (__int16*)tabptr ) + 1 ), 0, name_entry_size - 2 );
				name = name->TrimEnd( '\0' );

				match->SetGroupName( n, name );

				tabptr += name_entry_size;
			}
		}

		return match;
	}


	void Matcher::BuildOptions( )
	{
#define C(f, n) \
	list->Add(gcnew OptionInfo( f, gcnew String(#f), gcnew String(n)));

		List<OptionInfo^>^ list = gcnew List<OptionInfo^>( );

		C( PCRE2_ANCHORED, "Force pattern anchoring" );
		C( PCRE2_ALLOW_EMPTY_CLASS, "Allow empty classes" );
		C( PCRE2_ALT_BSUX, "Alternative handling of \\u, \\U, and \\x" );
		C( PCRE2_ALT_CIRCUMFLEX, "Alternative handling of ^ in multiline mode" );
		C( PCRE2_ALT_VERBNAMES, "Process backslashes in verb names" );
		//C( PCRE2_AUTO_CALLOUT, "Compile automatic callouts" );
		C( PCRE2_CASELESS, "Do caseless matching" );
		C( PCRE2_DOLLAR_ENDONLY, "$ not to match newline at end" );
		C( PCRE2_DOTALL, ". matches anything including NL" );
		C( PCRE2_DUPNAMES, "Allow duplicate names for subpatterns" );
		C( PCRE2_ENDANCHORED, "Pattern can match only at end of subject" );
		C( PCRE2_EXTENDED, "Ignore white space and # comments" );
		C( PCRE2_FIRSTLINE, "Force matching to be before newline" );
		C( PCRE2_LITERAL, "Pattern characters are all literal" );
		//C( PCRE2_MATCH_INVALID_UTF, "Enable support for matching invalid UTF" );
		C( PCRE2_MATCH_UNSET_BACKREF, "Match unset backreferences" );
		C( PCRE2_MULTILINE, "^ and $ match newlines within data" );
		C( PCRE2_NEVER_BACKSLASH_C, "Lock out the use of \\C in patterns" );
		C( PCRE2_NEVER_UCP, "Lock out PCRE2_UCP, e.g. via (*UCP)" );
		C( PCRE2_NEVER_UTF, "Lock out PCRE2_UTF, e.g. via (*UTF)" );
		C( PCRE2_NO_AUTO_CAPTURE, "Disable numbered capturing parentheses (named ones available)" );
		C( PCRE2_NO_AUTO_POSSESS, "Disable auto-possessification" );
		C( PCRE2_NO_DOTSTAR_ANCHOR, "Disable automatic anchoring for .*" );
		C( PCRE2_NO_START_OPTIMIZE, "Disable match-time start optimizations" );
		//C( PCRE2_NO_UTF_CHECK, "Do not check the pattern for UTF validity (only relevant if PCRE2_UTF is set)" );
		C( PCRE2_UCP, "Use Unicode properties for \\d, \\w, etc." );
		C( PCRE2_UNGREEDY, "Invert greediness of quantifiers" );
		C( PCRE2_USE_OFFSET_LIMIT, "Enable offset limit for unanchored matching" );
		//C( PCRE2_UTF, "Treat pattern and subjects as UTF strings" );

		mCompileOptions = list;

		list = gcnew List<OptionInfo^>( );

		C( PCRE2_EXTRA_ALLOW_SURROGATE_ESCAPES, "Allow \\x{df800} to \\x{dfff} in UTF-8 and UTF-32 modes" );
		C( PCRE2_EXTRA_ALT_BSUX, "Extended alternate \\u, \\U, and \\x handling" );
		C( PCRE2_EXTRA_BAD_ESCAPE_IS_LITERAL, "Treat all invalid escapes as a literal following character" );
		C( PCRE2_EXTRA_ESCAPED_CR_IS_LF, "Interpret \\r as \\n" );
		C( PCRE2_EXTRA_MATCH_LINE, "Pattern matches whole lines" );
		C( PCRE2_EXTRA_MATCH_WORD, "Pattern matches \"words\"" );

		mExtraCompileOptions = list;

		list = gcnew List<OptionInfo^>( );

		C( PCRE2_ANCHORED, "Match only at the first position" );
		C( PCRE2_COPY_MATCHED_SUBJECT, "On success, make a private subject copy" );
		C( PCRE2_ENDANCHORED, "Pattern can match only at end of subject" );
		C( PCRE2_NOTBOL, "Subject string is not the beginning of a line" );
		C( PCRE2_NOTEOL, "Subject string is not the end of a line" );
		C( PCRE2_NOTEMPTY, "An empty string is not a valid match" );
		C( PCRE2_NOTEMPTY_ATSTART, "An empty string at the start of the subject is not a valid match" );
		C( PCRE2_NO_JIT, "Do not use JIT matching" );
		//C( PCRE2_NO_UTF_CHECK, "Do not check the subject for UTF validity (only relevant if PCRE2_UTF was set at compile time)" );
		C( PCRE2_PARTIAL_HARD, "Return PCRE2_ERROR_PARTIAL for a partial match even if there is a full match" );
		C( PCRE2_PARTIAL_SOFT, "Return PCRE2_ERROR_PARTIAL for a partial match if no full matches are found" );
		C( PCRE2_DFA_SHORTEST, "Return only the shortest match" );

		mMatchOptions = list;

#undef C
	}
}
