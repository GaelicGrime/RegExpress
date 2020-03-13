#include "pch.h"

#include "Matcher.h"
#include "OnigurumaHelper.h"


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

		msclr::auto_handle< OnigurumaHelper> helper( CreateOnigurumaHelper( options ) );

		auto search_options = ONIG_OPTION_NONE;

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

		pin_ptr<const wchar_t> pinned_pattern = PtrToStringChars( pattern );
		const wchar_t* native_pattern = pinned_pattern;

		r = onig_new(
			&reg,
			(UChar*)native_pattern,
			(UChar*)( native_pattern + pattern->Length ),
			helper->GetCompileOptions( ),
			ONIG_ENCODING_UTF16_LE,
			helper->GetSyntax( ),
			&einfo );

		if( r )
		{
			throw gcnew Exception( FormatError( r, &einfo ) );
		}

		mData = new MatcherData{};
		mData->mRegex = reg;
		mData->mSearchOptions = search_options;
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


	OnigurumaHelper^ Matcher::CreateOnigurumaHelper( cli::array<String^>^ options )
	{
		String^ selected_syntax_name = L"ONIG_SYNTAX_ONIGURUMA";
		auto selected_syntax = ONIG_SYNTAX_ONIGURUMA;

		for each( auto o in mSyntaxOptions )
		{
			if( Array::IndexOf( options, o->FlagName ) >= 0 )
			{
				IntPtr p;

				if( mTagToOption->TryGetValue( o->FlagName, p ) )
				{
					selected_syntax_name = o->FlagName;
					selected_syntax = static_cast<decltype( selected_syntax )>( p.ToPointer( ) );
				}
				else
				{
					Debug::Assert( false );
				}

				break;
			}
		}

		OnigOptionType compile_options = ONIG_OPTION_NONE;

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

		return gcnew OnigurumaHelper( selected_syntax_name, adjusted_syntax, compile_options );
	}


	String^ Matcher::GetVersion( )
	{
		return gcnew String( onig_version( ) );
	}


	private value struct ForEachNameData
	{
		List<String^>^ names;
	};


	static int ForEachNameCallback( const OnigUChar* name, const OnigUChar* nameEnd,
		int numberOfGroups, int* groupNumberList, OnigRegex regex, void* lparam )
	{
		ForEachNameData* data = (ForEachNameData*)lparam;

		String^ group_name = gcnew String( (wchar_t*)name, 0, CheckedCast::ToInt32( ( (wchar_t*)nameEnd ) - ( (wchar_t*)name ) ) );

		int* nums;
		int r = onig_name_to_group_numbers( regex, name, nameEnd, &nums );

		for( int i = 0; i < r; ++i )
		{
			int group_number = nums[i];

			while( data->names->Count <= group_number ) data->names->Add( nullptr );

			data->names[group_number] = group_name;
		}

		return 0;
	}


	private value struct TraverseTreeData
	{
		int groupNumber;
		SimpleGroup^ group;
	};


	static int TraverseTreeCallback( int group, int beg, int end, int level, int at, void* arg )
	{
		TraverseTreeData* data = (TraverseTreeData*)arg;

		if( group == 0 )
		{
			// skip, not needed
		}
		else
		{
			if( group == data->groupNumber )
			{
				Debug::Assert( ( beg % 2 ) == 0 ); // event positions expected
				Debug::Assert( ( end % 2 ) == 0 );

				data->group->AddCapture( beg / 2, ( end - beg ) / 2 );
			}
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
				ForEachNameData data{};
				data.names = group_names;

				onig_foreach_name( mData->mRegex, &ForEachNameCallback, &data );
			}


			pin_ptr<const wchar_t> pinned_text = PtrToStringChars( text );
			const wchar_t* native_text = pinned_text;

			auto matches = gcnew List<IMatch^>;

			OnigMatchParam* match_params = onig_new_match_param( );
			OnigRegion* region = onig_region_new( );

			try
			{
				// currently default parameters are used
				onig_initialize_match_param( match_params );
				//int rl = onig_get_retry_limit_in_match( );
				//int sl = onig_get_match_stack_limit_size( );
				//onig_set_match_stack_limit_size_of_match_param ( OnigMatchParam * param, unsigned int limit );
				//onig_set_retry_limit_in_match_of_match_param ( OnigMatchParam * param, unsigned long limit );

				int r;
				const wchar_t* start = native_text;
				const wchar_t* previous_start = start;

				for( ;;)
				{
					r = onig_search_with_param(
						mData->mRegex,
						(UChar*)native_text, (UChar*)( native_text + text->Length ),
						(UChar*)start, (UChar*)( native_text + text->Length ),
						region,
						mData->mSearchOptions,
						match_params );

					if( r == ONIG_MISMATCH ) break;

					if( r < 0 )
					{
						throw gcnew Exception( FormatError( r, nullptr ) );
					}


					SimpleMatch^ match0 = nullptr;

					for( int i = 0; i < region->num_regs; ++i )
					{
						int group_number = i;
						String^ group_name = nullptr;
						if( group_number < group_names->Count ) group_name = group_names[group_number];
						if( group_name == nullptr ) group_name = group_number.ToString( System::Globalization::CultureInfo::InvariantCulture );

						if( region->beg[i] < 0 )
						{
							// failed group

							if( i == 0 )
							{
								Debug::Assert( false );
							}
							else
							{
								match0->AddGroup( 0, 0, false, group_name );
							}
						}
						else
						{
							// succeeded group

							Debug::Assert( ( region->beg[i] % sizeof( wchar_t ) ) == 0 ); // even positions expected
							Debug::Assert( ( region->end[i] % sizeof( wchar_t ) ) == 0 );

							int begin = region->beg[i] / sizeof( wchar_t );
							int end = region->end[i] / sizeof( wchar_t );

							if( i == 0 )
							{
								match0 = SimpleMatch::Create( begin, end - begin, this );
								matches->Add( match0 );
							}

							SimpleGroup^ group = match0->AddGroup( begin, end - begin, true, group_name );

							// captures

							int number_of_captures = onig_number_of_captures( mData->mRegex );
							int number_of_capture_histories = onig_number_of_capture_histories( mData->mRegex );
							OnigCaptureTreeNode* capture_tree = onig_get_capture_tree( region );

							{
								TraverseTreeData data{};
								data.groupNumber = group_number;
								data.group = group;

								onig_capture_tree_traverse( region, ONIG_TRAVERSE_CALLBACK_AT_FIRST, &TraverseTreeCallback, &data );
							}
						}
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
				onig_free_match_param( match_params );
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
		return OriginalText->Substring( index, length );
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
			return String::Format( "{0}\r\n\r\n({1}, {2})", text, symbol, code );
		}
		else
		{
			return String::Format( "{0}\r\n\r\n({1})", text, code );
		}
	}



	IMatch^ Matcher::CreateMatch( OnigRegion* region, List<String^>^ groupNames )
	{
		SimpleMatch^ match = nullptr;

		for( int i = 0; i < region->num_regs; ++i )
		{
			int group_number = i;
			String^ group_name = nullptr;
			if( group_number < groupNames->Count ) group_name = groupNames[group_number];
			if( group_name == nullptr ) group_name = group_number.ToString( System::Globalization::CultureInfo::InvariantCulture );

			if( region->beg[i] < 0 )
			{
				// failed group

				if( i == 0 )
				{
					Debug::Assert( false );
				}
				else
				{
					match->AddGroup( 0, 0, false, group_name );
				}
			}
			else
			{
				// succeeded group

				Debug::Assert( ( region->beg[i] % sizeof( wchar_t ) ) == 0 ); // even positions expected
				Debug::Assert( ( region->end[i] % sizeof( wchar_t ) ) == 0 );

				int begin = region->beg[i] / sizeof( wchar_t );
				int end = region->end[i] / sizeof( wchar_t );

				if( i == 0 )
				{
					match = SimpleMatch::Create( begin, end - begin, this );
				}

				SimpleGroup^ group = match->AddGroup( begin, end - begin, true, group_name );

				// captures

				int number_of_captures = onig_number_of_captures( mData->mRegex );
				int number_of_capture_histories = onig_number_of_capture_histories( mData->mRegex );
				OnigCaptureTreeNode* capture_tree = onig_get_capture_tree( region );

				{
					TraverseTreeData data{};
					data.groupNumber = group_number;
					data.group = group;

					onig_capture_tree_traverse( region, ONIG_TRAVERSE_CALLBACK_AT_FIRST, &TraverseTreeCallback, &data );
				}
			}
		}

		return match;
	}


	static IntPtr ToIntPtr( unsigned int i ) { return IntPtr( CheckedCast::ToInt32( i ) ); }
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

		//C( ONIG_SYN_OP_ESC_ASTERISK_ZERO_INF, "enable \\*" );
		//C( ONIG_SYN_OP_ESC_PLUS_ONE_INF, "enable \\+" );
		//C( ONIG_SYN_OP_ESC_QMARK_ZERO_ONE, "enable \\?" );
		//C( ONIG_SYN_OP_ESC_BRACE_INTERVAL, "enable \\{ and \\}" );
		//C( ONIG_SYN_OP_ESC_VBAR_ALT, "enable \\|" );
		//C( ONIG_SYN_OP_ESC_LPAREN_SUBEXP, "enable \\( and \\)" );
		//C( ONIG_SYN_OP_ESC_LTGT_WORD_BEGIN_END, "enable \\< and \\>" );
		//C( ONIG_SYN_OP_ESC_C_CONTROL, "enable \\cx" );
		//C( ONIG_SYN_OP_ESC_OCTAL3, "enable \\000" );
		//C( ONIG_SYN_OP_ESC_X_HEX2, "enable \\xHH" );
		//C( ONIG_SYN_OP_ESC_X_BRACE_HEX8, "enable \\x{HHH…}" );
		//C( ONIG_SYN_OP_ESC_O_BRACE_OCTAL, "enable \\o{OOO…}" );

		C( ONIG_SYN_OP2_ESC_CAPITAL_Q_QUOTE, "enable \\Q...\\E" );
		C( ONIG_SYN_OP2_ATMARK_CAPTURE_HISTORY, "enable (?@…) and (?@<name>…)" );
		C( ONIG_SYN_STRICT_CHECK_BACKREF, "error on invalid backrefs" ); //?

		mConfigurationOptions = list;


#undef C

	}
}
