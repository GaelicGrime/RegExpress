#include "pch.h"
#include "Matcher.h"
#include "Group.h"
#include "Match.h"


using namespace System::Diagnostics;
using namespace System::Runtime::InteropServices;
using namespace msclr::interop;


namespace OnigurumaRegexInterop
{

	static String^ FormatError( int code, const OnigErrorInfo* optionalEinfo );


	static Matcher::Matcher( )
	{
		mTagToOption = gcnew Dictionary<String^, IntPtr>;
		BuildOptions( );

		OnigEncoding use_encs[1];
		use_encs[0] = ONIG_ENCODING_UTF16_LE;

		int r = onig_initialize( use_encs, sizeof( use_encs ) / sizeof( use_encs[0] ) );

		if( r != 0 )
		{
			// TODO: get error message and report it
			//onig_error_code_to_str( )

			Debug::Assert( false );
		}
	}


	Matcher::Matcher( String^ pattern, cli::array<String^>^ options )
		:mData( nullptr )
	{
		regex_t* reg;
		OnigErrorInfo einfo;
		int r;

		auto selected_syntax = ONIG_SYNTAX_ONIGURUMA;
		auto compile_options = ONIG_OPTION_NONE;
		auto search_options = ONIG_OPTION_NONE;

		{
			// get options

			for each( auto o in mSyntaxOptions )
			{
				if( Array::IndexOf( options, o->FlagName ) >= 0 )
				{
					IntPtr p;

					if( mTagToOption->TryGetValue( o->FlagName, p ) )
					{
						selected_syntax = static_cast<decltype( selected_syntax )>( p.ToPointer( ) );
					}
					else
					{
						Debug::Assert( false );
					}

					break;
				}
			}

			for each( auto o in mCompileOptions )
			{
				if( Array::IndexOf( options, o->FlagName ) >= 0 )
				{
					IntPtr p;

					if( mTagToOption->TryGetValue( o->FlagName, p ) )
					{
						compile_options |= p.ToInt64( );
					}
					else
					{
						Debug::Assert( false );
					}
				}
			}

			for each( auto o in mSearchOptions )
			{
				if( Array::IndexOf( options, o->FlagName ) >= 0 )
				{
					IntPtr p;

					if( mTagToOption->TryGetValue( o->FlagName, p ) )
					{
						search_options |= p.ToInt64( );
					}
					else
					{
						Debug::Assert( false );
					}
				}
			}
		}

		// in some case, create custom syntax

		OnigSyntaxType adjusted_syntax{};

		onig_copy_syntax( &adjusted_syntax, selected_syntax );

		for each( auto o in mConfigurationOptions )
		{
			if( Array::IndexOf( options, o->FlagName ) >= 0 )
			{
				IntPtr p;

				if( mTagToOption->TryGetValue( o->FlagName, p ) )
				{
					if( o->FlagName->Contains( "_OP_" ) )
					{
						adjusted_syntax.op |= p.ToInt32( );
					}
					else if( o->FlagName->Contains( "_OP2_" ) )
					{
						adjusted_syntax.op2 |= p.ToInt32( );
					}
					else
					{
						adjusted_syntax.behavior |= p.ToInt32( );
					}
				}
			}
		}

		pin_ptr<const wchar_t> pinned_pattern = PtrToStringChars( pattern );
		const wchar_t* native_pattern = pinned_pattern;

		r = onig_new(
			&reg,
			(UChar*)native_pattern,
			(UChar*)( native_pattern + pattern->Length ),
			compile_options,
			ONIG_ENCODING_UTF16_LE,
			&adjusted_syntax,
			&einfo );

		if( r )
		{
			throw gcnew Exception( FormatError( r, &einfo ) );
		}

		mData = new MatcherData{};
		mData->mRegex = reg;
		mData->mSearchOptions = search_options;
		onig_copy_syntax( &mData->mSyntax, &adjusted_syntax );
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


	String^ Matcher::GetVersion( )
	{
		return gcnew String( onig_version( ) );
	}


	static int ForEachNameCallback( const OnigUChar* name, const OnigUChar* nameEnd,
		int numberOfGroups, int* groupNumberList, OnigRegex regex, void* lparam )
	{
		const IntPtr* intptr_names = (IntPtr*)lparam;
		GCHandle gch_names = GCHandle::FromIntPtr( *intptr_names );
		List<String^>^ names = ( List<String^>^ )gch_names.Target;

		String^ group_name = gcnew String( (wchar_t*)name, 0, ( (wchar_t*)nameEnd ) - ( (wchar_t*)name ) );

		int* nums;
		int r = onig_name_to_group_numbers( regex, name, nameEnd, &nums );

		for( int i = 0; i < r; ++i )
		{
			int group_number = nums[i];

			while( names->Count <= group_number ) names->Add( nullptr );

			names[group_number] = group_name;
		}

		return 0;
	}


	RegexMatches^ Matcher::Matches( String^ text )
	{
		try
		{
			OriginalText = text;


			// extract group names

			List<String^>^ group_names = gcnew List<String^>( );
			{
				GCHandle gch_names = GCHandle::Alloc( group_names );
				try
				{
					IntPtr intptr_names = GCHandle::ToIntPtr( gch_names );

					onig_foreach_name( mData->mRegex, &ForEachNameCallback, &intptr_names );
				}
				finally
				{
					gch_names.Free( );
				}
			}

			pin_ptr<const wchar_t> pinned_text = PtrToStringChars( text );
			const wchar_t* native_text = pinned_text;

			auto matches = gcnew List<IMatch^>;

			int r;
			OnigRegion* region = onig_region_new( );

			const wchar_t* start = native_text;
			const wchar_t* previous_start = start;

			try
			{
				for( ;;)
				{
					r = onig_search(
						mData->mRegex,
						(UChar*)native_text, (UChar*)( native_text + text->Length ),
						(UChar*)start, (UChar*)( native_text + text->Length ),
						region,
						mData->mSearchOptions );

					if( r == ONIG_MISMATCH ) break;

					if( r < 0 )
					{
						onig_region_free( region, 1 );

						throw gcnew Exception( FormatError( r, nullptr ) );
					}


					Match^ match0 = nullptr;

					for( int i = 0; i < region->num_regs; ++i )
					{
						Debug::Assert( ( region->beg[i] % sizeof( wchar_t ) ) == 0 ); // even positions expected
						Debug::Assert( ( region->end[i] % sizeof( wchar_t ) ) == 0 );

						int begin = region->beg[i] / sizeof( wchar_t );
						int end = region->end[i] / sizeof( wchar_t );

						if( i == 0 )
						{
							match0 = gcnew Match( this, begin, end - begin );

							matches->Add( match0 );
						}

						int group_number = i;
						String^ group_name = nullptr;
						if( group_number < group_names->Count ) group_name = group_names[group_number];
						if( group_name == nullptr ) group_name = group_number.ToString( System::Globalization::CultureInfo::InvariantCulture );

						match0->AddGroup( gcnew Group( match0, group_name, true, begin, end - begin ) ); // (including default group)
					}

					// TODO: check if it should be much more complicated -- see PCRE2

					start = native_text + match0->Index + match0->Length;

					if( start == previous_start )
					{
						++start;
					}

					previous_start = start;
				}
			}
			finally
			{
				onig_region_free( region, 1 );
			}

			return gcnew RegexMatches( matches->Count, matches );
		}
		//catch( const std::exception & exc )
		//{
		//	String^ what = gcnew String( exc.what( ) );
		//	throw gcnew Exception( "Error: " + what );
		//}
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


	static const char* TryGetErrorSymbol0( int code )
	{

#define C(c) if( code == c) return #c;

		C( ONIG_MISMATCH );
		C( ONIG_NO_SUPPORT_CONFIG );
		C( ONIG_ABORT );
		C( ONIGERR_MEMORY );
		C( ONIGERR_TYPE_BUG );
		C( ONIGERR_PARSER_BUG );
		C( ONIGERR_STACK_BUG );
		C( ONIGERR_UNDEFINED_BYTECODE );
		C( ONIGERR_UNEXPECTED_BYTECODE );
		C( ONIGERR_MATCH_STACK_LIMIT_OVER );
		C( ONIGERR_PARSE_DEPTH_LIMIT_OVER );
		C( ONIGERR_RETRY_LIMIT_IN_MATCH_OVER );
		C( ONIGERR_DEFAULT_ENCODING_IS_NOT_SETTED );
		C( ONIGERR_SPECIFIED_ENCODING_CANT_CONVERT_TO_WIDE_CHAR );
		C( ONIGERR_FAIL_TO_INITIALIZE );
		C( ONIGERR_INVALID_ARGUMENT );
		C( ONIGERR_END_PATTERN_AT_LEFT_BRACE );
		C( ONIGERR_END_PATTERN_AT_LEFT_BRACKET );
		C( ONIGERR_EMPTY_CHAR_CLASS );
		C( ONIGERR_PREMATURE_END_OF_CHAR_CLASS );
		C( ONIGERR_END_PATTERN_AT_ESCAPE );
		C( ONIGERR_END_PATTERN_AT_META );
		C( ONIGERR_END_PATTERN_AT_CONTROL );
		C( ONIGERR_META_CODE_SYNTAX );
		C( ONIGERR_CONTROL_CODE_SYNTAX );
		C( ONIGERR_CHAR_CLASS_VALUE_AT_END_OF_RANGE );
		C( ONIGERR_CHAR_CLASS_VALUE_AT_START_OF_RANGE );
		C( ONIGERR_UNMATCHED_RANGE_SPECIFIER_IN_CHAR_CLASS );
		C( ONIGERR_TARGET_OF_REPEAT_OPERATOR_NOT_SPECIFIED );
		C( ONIGERR_TARGET_OF_REPEAT_OPERATOR_INVALID );
		C( ONIGERR_NESTED_REPEAT_OPERATOR );
		C( ONIGERR_UNMATCHED_CLOSE_PARENTHESIS );
		C( ONIGERR_END_PATTERN_WITH_UNMATCHED_PARENTHESIS );
		C( ONIGERR_END_PATTERN_IN_GROUP );
		C( ONIGERR_UNDEFINED_GROUP_OPTION );
		C( ONIGERR_INVALID_POSIX_BRACKET_TYPE );
		C( ONIGERR_INVALID_LOOK_BEHIND_PATTERN );
		C( ONIGERR_INVALID_REPEAT_RANGE_PATTERN );
		C( ONIGERR_TOO_BIG_NUMBER );
		C( ONIGERR_TOO_BIG_NUMBER_FOR_REPEAT_RANGE );
		C( ONIGERR_UPPER_SMALLER_THAN_LOWER_IN_REPEAT_RANGE );
		C( ONIGERR_EMPTY_RANGE_IN_CHAR_CLASS );
		C( ONIGERR_MISMATCH_CODE_LENGTH_IN_CLASS_RANGE );
		C( ONIGERR_TOO_MANY_MULTI_BYTE_RANGES );
		C( ONIGERR_TOO_SHORT_MULTI_BYTE_STRING );
		C( ONIGERR_TOO_BIG_BACKREF_NUMBER );
		C( ONIGERR_INVALID_BACKREF );
		C( ONIGERR_NUMBERED_BACKREF_OR_CALL_NOT_ALLOWED );
		C( ONIGERR_TOO_MANY_CAPTURES );
		C( ONIGERR_TOO_LONG_WIDE_CHAR_VALUE );
		C( ONIGERR_EMPTY_GROUP_NAME );
		C( ONIGERR_INVALID_GROUP_NAME );
		C( ONIGERR_INVALID_CHAR_IN_GROUP_NAME );
		C( ONIGERR_UNDEFINED_NAME_REFERENCE );
		C( ONIGERR_UNDEFINED_GROUP_REFERENCE );
		C( ONIGERR_MULTIPLEX_DEFINED_NAME );
		C( ONIGERR_MULTIPLEX_DEFINITION_NAME_CALL );
		C( ONIGERR_NEVER_ENDING_RECURSION );
		C( ONIGERR_GROUP_NUMBER_OVER_FOR_CAPTURE_HISTORY );
		C( ONIGERR_INVALID_CHAR_PROPERTY_NAME );
		C( ONIGERR_INVALID_IF_ELSE_SYNTAX );
		C( ONIGERR_INVALID_ABSENT_GROUP_PATTERN );
		C( ONIGERR_INVALID_ABSENT_GROUP_GENERATOR_PATTERN );
		C( ONIGERR_INVALID_CALLOUT_PATTERN );
		C( ONIGERR_INVALID_CALLOUT_NAME );
		C( ONIGERR_UNDEFINED_CALLOUT_NAME );
		C( ONIGERR_INVALID_CALLOUT_BODY );
		C( ONIGERR_INVALID_CALLOUT_TAG_NAME );
		C( ONIGERR_INVALID_CALLOUT_ARG );
		C( ONIGERR_INVALID_CODE_POINT_VALUE );
		C( ONIGERR_INVALID_WIDE_CHAR_VALUE );
		C( ONIGERR_TOO_BIG_WIDE_CHAR_VALUE );
		C( ONIGERR_NOT_SUPPORTED_ENCODING_COMBINATION );
		C( ONIGERR_INVALID_COMBINATION_OF_OPTIONS );
		C( ONIGERR_TOO_MANY_USER_DEFINED_OBJECTS );
		C( ONIGERR_TOO_LONG_PROPERTY_NAME );
		C( ONIGERR_LIBRARY_IS_NOT_INITIALIZED );

#undef C

		return nullptr;
	}


	static String^ TryGetErrorSymbol( int code )
	{
		const char* s = TryGetErrorSymbol0( code );

		return s == nullptr ? nullptr : gcnew String( s );
	}


	static String^ FormatError( int code, const OnigErrorInfo* optionalEinfo )
	{
		char s[ONIG_MAX_ERROR_MESSAGE_LEN];
		onig_error_code_to_str( (UChar*)s, code, optionalEinfo );
		String^ text = gcnew String( s );

		String^ symbol = TryGetErrorSymbol( code );

		if( symbol != nullptr )
		{
			return String::Format( "{0}\r\n({1}, {2})", text, symbol, code );
		}
		else
		{
			return String::Format( "{0}\r\n({1})", text, code );
		}
	}


	static IntPtr ToIntPtr( unsigned int i ) { return IntPtr( (int)i ); }
	static IntPtr ToIntPtr( void* p ) { return IntPtr( p ); }


	void Matcher::BuildOptions( )
	{

#define C(f, n) \
	list->Add(gcnew OptionInfo( gcnew String(#f), gcnew String(n))); \
	mTagToOption[gcnew String(#f)] = ToIntPtr(f);

		List<OptionInfo^>^ list = gcnew List<OptionInfo^>( );


		C( ONIG_SYNTAX_ONIGURUMA, "Oniguruma" );
		C( ONIG_SYNTAX_ASIS, "plain text" );
		C( ONIG_SYNTAX_POSIX_BASIC, "POSIX Basic RE" );
		C( ONIG_SYNTAX_POSIX_EXTENDED, "POSIX Extended RE" );
		C( ONIG_SYNTAX_EMACS, "Emacs" );
		C( ONIG_SYNTAX_GREP, "grep" );
		C( ONIG_SYNTAX_GNU_REGEX, "GNU regex" );
		C( ONIG_SYNTAX_JAVA, "Java( Sun java.util.regex )" );
		C( ONIG_SYNTAX_PERL, "Perl" );
		C( ONIG_SYNTAX_PERL_NG, "Perl + named group" );
		C( ONIG_SYNTAX_RUBY, "Ruby" );

		mSyntaxOptions = list;


		list = gcnew List<OptionInfo^>( );

		C( ONIG_OPTION_SINGLELINE, "'^' -> '\\A', '$' -> '\\Z'" );
		C( ONIG_OPTION_MULTILINE, "'.' match with newline" );
		C( ONIG_OPTION_IGNORECASE, "ambiguity match on" );
		C( ONIG_OPTION_EXTEND, "extended pattern form" );
		C( ONIG_OPTION_FIND_LONGEST, "find longest match" );
		C( ONIG_OPTION_FIND_NOT_EMPTY, "ignore empty match" );
		C( ONIG_OPTION_NEGATE_SINGLELINE, "clear ONIG_OPTION_SINGLELINE" );
		C( ONIG_OPTION_DONT_CAPTURE_GROUP, "only named group captured" );
		C( ONIG_OPTION_CAPTURE_GROUP, "named and no-named group captured" );
		C( ONIG_OPTION_WORD_IS_ASCII, "ASCII only word (\\w, \\p{Word}, [[:word:]]), ASCII only word bound (\\b)" );
		C( ONIG_OPTION_DIGIT_IS_ASCII, "ASCII only digit (\\d, \\p{Digit}, [[:digit:]])" );
		C( ONIG_OPTION_SPACE_IS_ASCII, "ASCII only space (\\s, \\p{Space}, [[:space:]])" );
		C( ONIG_OPTION_POSIX_IS_ASCII, "ASCII only POSIX properties" );
		C( ONIG_OPTION_TEXT_SEGMENT_EXTENDED_GRAPHEME_CLUSTER, "Extended Grapheme Cluster mode" );
		C( ONIG_OPTION_TEXT_SEGMENT_WORD, "Word mode" );

		mCompileOptions = list;


		list = gcnew List<OptionInfo^>( );

		C( ONIG_OPTION_NOTBOL, "string head( str ) isn't considered as begin of line" );
		C( ONIG_OPTION_NOTEOL, "string end( end ) isn't considered as end of line" );
		//C( ONIG_OPTION_POSIX_REGION, "region argument is regmatch_t[] of POSIX API" );

		mSearchOptions = list;


		list = gcnew List<OptionInfo^>( );

		C( ONIG_SYN_OP_ESC_ASTERISK_ZERO_INF, "enable \\*" );
		C( ONIG_SYN_OP2_ATMARK_CAPTURE_HISTORY, "enable (?@…) and (?@<name>…)" );
		C( ONIG_SYN_STRICT_CHECK_BACKREF, "error on invalid backrefs" ); //?

		mConfigurationOptions = list;


#undef C

	}
}
