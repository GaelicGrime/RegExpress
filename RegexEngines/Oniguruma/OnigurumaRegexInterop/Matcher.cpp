#include "pch.h"
#include "Matcher.h"
#include "Group.h"
#include "Match.h"


using namespace System::Diagnostics;
using namespace msclr::interop;


namespace OnigurumaRegexInterop
{

	static String^ FormatError( int code, const OnigErrorInfo* optionalEinfo );


	static Matcher::Matcher( )
	{
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

		pin_ptr<const wchar_t> pinned_pattern = PtrToStringChars( pattern );
		const wchar_t* native_pattern = pinned_pattern;

		r = onig_new( &reg,
			(UChar*)native_pattern,
			(UChar*)( native_pattern + pattern->Length ),
			ONIG_OPTION_DEFAULT, ONIG_ENCODING_UTF16_LE, ONIG_SYNTAX_DEFAULT, &einfo );
		if( r )
		{
			throw gcnew Exception( FormatError( r, &einfo ) );
		}

		mData = new MatcherData{};
		mData->mRegex = reg;
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


	RegexMatches^ Matcher::Matches( String^ text )
	{
		try
		{
			OriginalText = text;

			pin_ptr<const wchar_t> pinned_text = PtrToStringChars( text );
			const wchar_t* native_text = pinned_text;

			int r;
			OnigRegion* region = onig_region_new( );

			r = onig_search( mData->mRegex,
				(UChar*)native_text, (UChar*)( native_text + text->Length ),
				(UChar*)native_text, (UChar*)( native_text + text->Length ),
				region, ONIG_OPTION_NONE );

			if( r == ONIG_MISMATCH )
			{
				onig_region_free( region, 1 );

				return gcnew RegexMatches( 0, gcnew List<IMatch^> );
			}

			if( r < 0 )
			{
				onig_region_free( region, 1 );

				throw gcnew Exception( FormatError( r, nullptr ) );
			}

			auto matches = gcnew List<IMatch^>;

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
					match0->AddGroup( gcnew Group( match0, "0", true, begin, end - begin ) ); // default group

					matches->Add( match0 );
				}

				//...................
			}

			// TODO: find next matches

			onig_region_free( region, 1 ); // TODO: free even in case of exceptions

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
			return String::Format( "{0} ({1}):\r\n{2}", symbol, code, text );
		}
		else
		{
			return String::Format( "Error {0}:\r\n{1}", code, text );
		}
	}
}
