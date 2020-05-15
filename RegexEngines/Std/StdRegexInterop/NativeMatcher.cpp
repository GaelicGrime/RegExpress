#include <Windows.h>
#include <strsafe.h>
#include <exception>
#include "NativeMatcher.h"


namespace StdRegexInterop
{
	long Variable_REGEX_MAX_STACK_COUNT = Default_REGEX_MAX_STACK_COUNT;
	long Variable_REGEX_MAX_COMPLEXITY_COUNT = Default_REGEX_MAX_COMPLEXITY_COUNT;


	struct NativeMatcherData
	{
		std::vector<NativeMatch>* matches;
		const wchar_t* begin;
		const wchar_t* end;
		std::wregex* regex;
		std::regex_constants::match_flag_type flags;
		char* errorText;
		size_t errorTextSize;
	};


	static void NativeMatches0( NativeMatcherData* data )
	{
		std::wcregex_iterator results_begin( data->begin, data->end, *data->regex, data->flags );
		std::wcregex_iterator results_end{};

		for( auto i = results_begin; i != results_end; ++i )
		{
			const std::wcmatch& match = *i;

			data->matches->push_back( NativeMatch{ NativeMatch::TypeEnum::M, match.position( ), match.length( ) } );

			int j = 0;

			for( auto i = match.cbegin( ); i != match.cend( ); ++i, ++j )
			{
				const std::wcsub_match& submatch = *i;

				if( !submatch.matched )
				{
					data->matches->push_back( NativeMatch{ NativeMatch::TypeEnum::G, -1, -1 } );
				}
				else
				{
					data->matches->push_back( NativeMatch{ NativeMatch::TypeEnum::G, match.position( j ), match.length( j ) } );
				}
			}
		}
	}


	static DWORD SEHFilter( DWORD code, char* errorText, size_t errorTextSize )
	{
		const char* text;

		switch( code )
		{

#define E(e) case e: text = #e; break;

			E( EXCEPTION_ACCESS_VIOLATION )
				E( EXCEPTION_DATATYPE_MISALIGNMENT )
				E( EXCEPTION_BREAKPOINT )
				E( EXCEPTION_SINGLE_STEP )
				E( EXCEPTION_ARRAY_BOUNDS_EXCEEDED )
				E( EXCEPTION_FLT_DENORMAL_OPERAND )
				E( EXCEPTION_FLT_DIVIDE_BY_ZERO )
				E( EXCEPTION_FLT_INEXACT_RESULT )
				E( EXCEPTION_FLT_INVALID_OPERATION )
				E( EXCEPTION_FLT_OVERFLOW )
				E( EXCEPTION_FLT_STACK_CHECK )
				E( EXCEPTION_FLT_UNDERFLOW )
				E( EXCEPTION_INT_DIVIDE_BY_ZERO )
				E( EXCEPTION_INT_OVERFLOW )
				E( EXCEPTION_PRIV_INSTRUCTION )
				E( EXCEPTION_IN_PAGE_ERROR )
				E( EXCEPTION_ILLEGAL_INSTRUCTION )
				E( EXCEPTION_NONCONTINUABLE_EXCEPTION )
				E( EXCEPTION_STACK_OVERFLOW )
				E( EXCEPTION_INVALID_DISPOSITION )
				E( EXCEPTION_GUARD_PAGE )
				E( EXCEPTION_INVALID_HANDLE )
				//?E( EXCEPTION_POSSIBLE_DEADLOCK         )

#undef E

		default:
			return EXCEPTION_CONTINUE_SEARCH; // also covers code E06D7363, probably associated with 'throw std::exception'
		}

		StringCbCopyA( errorText, errorTextSize, "SEH Error: " );
		StringCbCatA( errorText, errorTextSize, text );

		return EXCEPTION_EXECUTE_HANDLER;
	}


	static void NativeMatchesSEH( NativeMatcherData* data )
	{
		DWORD code;

		__try
		{
			NativeMatches0( data );
		}
		__except( code = GetExceptionCode( ), SEHFilter( code, data->errorText, data->errorTextSize ) )
		{
			// done in filter
		}
	}


	static void NativeMatchesTryCatch( NativeMatcherData* data )
	{
		try
		{
			NativeMatchesSEH( data );
		}
		catch( const std::exception& exc )
		{
			const char* what = exc.what( );
			StringCbCopyA( data->errorText, data->errorTextSize, what );
		}
		catch( ... )
		{
			StringCbCopyA( data->errorText, data->errorTextSize, "Unknown error" );
		}

	}


	static DWORD WINAPI NativeMatchesThreadProc( LPVOID p )
	{
		NativeMatcherData* data = (NativeMatcherData*)p;

		NativeMatchesTryCatch( data );

		return 0;
	}


	void NativeMatches( std::vector<NativeMatch>* matches, long aREGEX_MAX_STACK_COUNT, long aREGEX_MAX_COMPLEXITY_COUNT,
		const wchar_t* begin, const wchar_t* end, std::wregex& regex, std::regex_constants::match_flag_type flags,
		char* errorText, size_t errorTextSize )
	{
		NativeMatcherData data;
		data.matches = matches;
		data.begin = begin;
		data.end = end;
		data.regex = &regex;
		data.flags = flags;
		data.errorText = errorText;
		data.errorTextSize = errorTextSize;

		Variable_REGEX_MAX_STACK_COUNT = aREGEX_MAX_STACK_COUNT;
		Variable_REGEX_MAX_COMPLEXITY_COUNT = aREGEX_MAX_COMPLEXITY_COUNT;

		*errorText = 0;

		HANDLE hThread = CreateThread( NULL, 0, &NativeMatchesThreadProc, &data, 0, NULL );

		switch( WaitForSingleObject( hThread, 45000 ) )
		{
		case WAIT_OBJECT_0:
			// success
			break;
		case WAIT_TIMEOUT:
			StringCbCopyA( errorText, errorTextSize, "The time-out interval elapsed." );
			break;
		case WAIT_FAILED:
			StringCbCopyA( errorText, errorTextSize, "The operation failed." );
			break;
		default:
			StringCbCopyA( errorText, errorTextSize, "The operation failed. Unknown error." );
			break;
		}

		CloseHandle( hThread );
	}

}
