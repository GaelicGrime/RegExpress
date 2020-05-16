#pragma warning( disable : 26812 )

#include <Windows.h>
#include <strsafe.h>
#include <process.h>
//#include <intrin.h>		 

#include <exception>
#include <atomic>
#include <cassert>

#include "NativeMatcher.h"


namespace StdRegexInterop
{
	thread_local long Variable_REGEX_MAX_STACK_COUNT = Default_REGEX_MAX_STACK_COUNT;
	thread_local long Variable_REGEX_MAX_COMPLEXITY_COUNT = Default_REGEX_MAX_COMPLEXITY_COUNT;


	struct NativeMatcherData
	{
	private:

		std::atomic_int refcount;
		std::atomic_flag locker;

	public:

		std::atomic_bool stop;

		std::wregex regex;
		std::wstring text;
		std::regex_constants::match_flag_type flags;
		long mREGEX_MAX_STACK_COUNT;
		long mREGEX_MAX_COMPLEXITY_COUNT;

		std::vector<NativeMatch> matches;
		char errorText[256];


		NativeMatcherData( )
			:
			refcount( 1 ),
			flags( std::regex_constants::match_flag_type::match_default ),
			locker( ),
			stop( ),
			mREGEX_MAX_STACK_COUNT( ),
			mREGEX_MAX_COMPLEXITY_COUNT( )
		{
			errorText[0] = '\0';
		}

		void addref( )
		{
			++refcount;
		}

		void release( )
		{
			if( --refcount == 0 ) delete this;
		}

		auto dbg_refcount( )
		{
			return refcount.load( );
		}

		void enter( )
		{
			while( locker.test_and_set( std::memory_order_acquire ) ); // acquire lock, spin
		}

		void leave( )
		{
			locker.clear( std::memory_order_release ); // release lock
		}
	};


	static void NativeMatches0( NativeMatcherData* data )
	{
		if( data->stop ) return;

		std::wcregex_iterator results_begin( data->text.c_str( ), data->text.c_str( ) + data->text.length( ), data->regex, data->flags );
		std::wcregex_iterator results_end{};

		for( auto i = results_begin; i != results_end; ++i )
		{
			if( data->stop ) return;

			const std::wcmatch& match = *i;

			data->matches.push_back( NativeMatch{ NativeMatch::TypeEnum::M, match.position( ), match.length( ) } );

			int j = 0;

			for( auto i = match.cbegin( ); i != match.cend( ); ++i, ++j )
			{
				if( data->stop ) return;

				const std::wcsub_match& submatch = *i;

				if( !submatch.matched )
				{
					data->matches.push_back( NativeMatch{ NativeMatch::TypeEnum::G, -1, -1 } );
				}
				else
				{
					data->matches.push_back( NativeMatch{ NativeMatch::TypeEnum::G, match.position( j ), match.length( j ) } );
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

		StringCchCopyA( errorText, errorTextSize, "SEH Error: " );
		StringCchCatA( errorText, errorTextSize, text );

		if( code == EXCEPTION_STACK_OVERFLOW )
		{
			/*
			StringCchCatA( errorText, errorTextSize, "\r\n(System stack limit: " );

			ULONG_PTR lo, hi;
			GetCurrentThreadStackLimits( &lo, &hi );

			size_t len;
			if( StringCchLengthA( errorText, errorTextSize, &len ) == S_OK )
			{
				_ui64toa_s( lo, errorText + len, errorTextSize - len - 1, 10 );
			}

			StringCchCatA( errorText, errorTextSize, ")" );
			*/

			/*
			StringCchCatA( errorText, errorTextSize, "\r\n(A: " );

			size_t len;
			if( StringCchLengthA( errorText, errorTextSize, &len ) == S_OK )
			{
				_ui64toa_s( ( unsigned long long )( (char*)some - (char*)&len ), errorText + len, errorTextSize - len - 1, 10 );
			}

			StringCchCatA( errorText, errorTextSize, ")" );
			*/

		}

		return EXCEPTION_EXECUTE_HANDLER;
	}


	static void NativeMatchesSEH( NativeMatcherData* data )
	{
		DWORD code;

		__try
		{
			NativeMatches0( data );
		}
		__except( code = GetExceptionCode( ), SEHFilter( code, data->errorText, _countof( data->errorText ) ) )
		{
			// things done in filter
		}
	}


	static void NativeMatchesTryCatch( NativeMatcherData* data )
	{
		try
		{
			Variable_REGEX_MAX_STACK_COUNT = data->mREGEX_MAX_STACK_COUNT;
			Variable_REGEX_MAX_COMPLEXITY_COUNT = data->mREGEX_MAX_COMPLEXITY_COUNT;

			NativeMatchesSEH( data );
		}
		catch( const std::exception& exc )
		{
			const char* what = exc.what( );
			StringCchCopyA( data->errorText, _countof( data->errorText ), what );
		}
		catch( ... )
		{
			StringCchCopyA( data->errorText, _countof( data->errorText ), "Unknown error" );
		}
	}


	static unsigned __stdcall NativeMatchesThreadProc( void* p )
	{
		ULONG ss = 1024 * 1;
		SetThreadStackGuarantee( &ss );

		NativeMatcherData* data = (NativeMatcherData*)p;

		NativeMatchesTryCatch( data );

		data->release( );

		_endthreadex( 0 );

		return 0; // (not achieved) 
	}


	//static DWORD WINAPI NativeMatchesThreadProc0( void* p )
	//{
	//	return NativeMatchesThreadProc( p );
	//}



	void NativeMatches( std::vector<NativeMatch>* matches, std::string* error, std::wregex& regex, const std::wstring& text, std::regex_constants::match_flag_type flags,
		long aREGEX_MAX_STACK_COUNT, long aREGEX_MAX_COMPLEXITY_COUNT, HANDLE cancelEvent )
	{
		NativeMatcherData* data = new NativeMatcherData( );

		data->regex = regex;
		data->text = text;
		data->flags = flags;
		data->mREGEX_MAX_STACK_COUNT = aREGEX_MAX_STACK_COUNT;
		data->mREGEX_MAX_COMPLEXITY_COUNT = aREGEX_MAX_COMPLEXITY_COUNT;

		matches->clear( );
		error->clear( );

		data->addref( ); // 2
		assert( data->dbg_refcount( ) == 2 );

		//HANDLE hThread = CreateThread( NULL, 0, &NativeMatchesThreadProc0, data, 0, NULL );
		auto thread = _beginthreadex( nullptr, 0, &NativeMatchesThreadProc, data, 0, nullptr );
		HANDLE hThread = (HANDLE)thread;

		HANDLE handles[] = { hThread, cancelEvent };


		switch( WaitForMultipleObjects( _countof( handles ), handles, FALSE, 60000 ) )
		{
		case WAIT_OBJECT_0 + 0:	// success
			*error = data->errorText;
			*matches = std::move( data->matches );
			break;
		case WAIT_OBJECT_0 + 1:	// cancel
			data->stop = true;
			*error = "Operation cancelled";
			//TerminateThread( hThread, 1 ); //
			break;
		case WAIT_TIMEOUT:
			data->stop = true;
			*error = "Operation takes long time to execute.";
			break;
		case WAIT_FAILED:
			*error = "The operation failed.";
			break;
		default:
			*error = "The operation failed. Unknown error.";
			break;
		}

		CloseHandle( hThread );

		data->release( );
	}

}
