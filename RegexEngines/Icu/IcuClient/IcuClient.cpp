// IcuClient.cpp : Defines the entry point for the application.
//

#include "pch.h"
#include "framework.h"
#include "IcuClient.h"


class Writer
{
public:

	Writer( HANDLE h )
		: mHandle( h )
	{
		assert( h != INVALID_HANDLE_VALUE );
		// (probably 0 is a valid handle)
	}


	void WriteString( const char* buffer, size_t size ) const
	{
		DWORD count;
		WriteFile( mHandle, buffer, size, &count, NULL );
	}


	void WriteString( LPCWSTR text ) const
	{
		WriteString( (const char*)text, lstrlenW( text ) * sizeof( WCHAR ) );
	}


	void __cdecl WriteString256( LPCWSTR format, ... ) const
	{
		wchar_t buffer[256];

		va_list argptr;
		va_start( argptr, format );

		StringCbVPrintfW( buffer, sizeof( buffer ), format, argptr );

		WriteString( buffer );

		va_end( argptr );
	}

private:

	HANDLE const mHandle;
};


struct Token
{
	LPCWSTR start; // null if no token
	size_t size;

	Token( )
	{
		start = nullptr;
		size = 0;
	}
};


class TokenReader final
{
public:

	TokenReader( LPCWSTR start, size_t size )
		: mStart( start ), mSize( size )
	{
		assert( mStart != nullptr );
		assert( mSize >= 0 );

		mCurrent = mSize == 0 ? nullptr : mStart;
		mRemained = mSize;
	}


	bool Read( Token* token ) const
	{
		assert( token != nullptr );
		assert( mRemained >= 0 );

		if( mRemained <= 0 )
		{
			token->start = nullptr;
			token->size = 0;

			return false;
		}
		else
		{
			assert( mRemained > 0 );

			token->start = mCurrent;

			LPCWSTR found_separator = (LPCWSTR)wmemchr( mCurrent, L'\0', mRemained );

			if( found_separator == nullptr )
			{
				token->size = mRemained;

				mRemained = 0;
			}
			else
			{
				assert( found_separator[0] == L'\0' );

				token->size = found_separator - mCurrent;

				assert( token->size >= 0 );

				mRemained -= token->size + 1;
				mCurrent = found_separator + 1;

				assert( mRemained >= 0 );
			}

			return true;
		}
	}

private:

	LPCWSTR const mStart;
	size_t const mSize;

	LPCWSTR mutable mCurrent;
	size_t mutable mRemained;
};


static bool Check( const Writer& errWriter, UErrorCode status )
{
	if( U_FAILURE( status ) )
	{
		LPCSTR error_name = u_errorName( status );

		errWriter.WriteString256( L"Error %hs (%u)", error_name, (unsigned)status );

		return false;
	}

	return true;
}


int APIENTRY wWinMain( _In_ HINSTANCE hInstance,
	_In_opt_ HINSTANCE hPrevInstance,
	_In_ LPWSTR    lpCmdLine,
	_In_ int       nCmdShow )
{
	SetDllDirectoryW( LR"(ICU-min\bin64)" );

	auto herr = GetStdHandle( STD_ERROR_HANDLE );
	if( herr == INVALID_HANDLE_VALUE )
	{
		auto lerr = GetLastError( );

		return 1;
	}

	Writer const errwr( herr );

	auto hin = GetStdHandle( STD_INPUT_HANDLE );
	if( hin == INVALID_HANDLE_VALUE )
	{
		errwr.WriteString( L"Cannot get STDIN" );

		return 2;
	}

	auto hout = GetStdHandle( STD_OUTPUT_HANDLE );
	if( hout == INVALID_HANDLE_VALUE )
	{
		errwr.WriteString( L"Cannot get STDOUT" );

		return 3;
	}

	Writer const outwr( hout );

	static char buffer[8 * 1024];
	char* inputB = NULL;
	size_t sizeB = 0;

	for( ;;)
	{
		DWORD count = 0;

		if( !ReadFile( hin, buffer, sizeof( buffer ), &count, NULL ) )
		{
			if( GetLastError( ) == ERROR_BROKEN_PIPE ) break;

			errwr.WriteString( L"Cannot read STDIN" );

			return 4;
		}

		if( count == 0 ) break;

		auto old = inputB;
		inputB = (char*)realloc( old, sizeB + count );

		if( inputB == NULL )
		{
			errwr.WriteString( L"Cannot realloc" );

			return 5;
		}

		memcpy( inputB + sizeB, buffer, count );

		sizeB += count;
	}

	if( ( sizeB % 2 ) != 0 )
	{
		errwr.WriteString( L"Invalid input length" );

		return 6;
	}

	//{
	//	auto old = inputB;
	//	inputB = (char*)realloc( old, sizeB + 2 );
	//	inputB[sizeB] = 0;
	//	inputB[sizeB + 1] = 0;
	//	sizeB += 2;
	//}


	size_t sizeW = sizeB / 2;
	LPCWSTR inputW = (LPCWSTR)inputB;

	TokenReader reader( inputW, sizeW );

	Token command_token;

	if( !reader.Read( &command_token ) )
	{
		errwr.WriteString( L"Cannot read command" );

		return 6;
	}


	if( command_token.size == 1 && command_token.start[0] == L'v' )
	{
		// get version

		auto v = L"" U_ICU_VERSION;

		outwr.WriteString( v );

		return 0;
	}


	if( command_token.size == 1 && command_token.start[0] == L'm' )
	{
		// find matches

		Token pattern_token;

		if( !reader.Read( &pattern_token ) )
		{
			errwr.WriteString( L"Cannot read pattern" );

			return 7;
		}

		Token text_token;

		if( !reader.Read( &text_token ) )
		{
			errwr.WriteString( L"Cannot read text" );

			return 8;
		}

		Token options_token;
		reader.Read( &options_token );

		Token timelimit_token;
		reader.Read( &timelimit_token );

		uint32_t icu_options = 0;

		UErrorCode status = U_ZERO_ERROR;
		UParseError parse_error{};

		icu::UnicodeString pattern( pattern_token.start, pattern_token.size );

		icu::RegexPattern* icu_pattern = icu::RegexPattern::compile( pattern, icu_options, parse_error, status );

		if( U_FAILURE( status ) )
		{
			LPCSTR error_name = u_errorName( status );

			errwr.WriteString256( L"Invalid pattern at line %i, column %i.\r\n\r\n(%hs, %u)",
				parse_error.line, parse_error.offset, error_name, (unsigned)status );

			return 9;
		}

		{
			// try identifying named groups; (ICU does not seem to offer such feature)

			icu::UnicodeString up( LR"REGEX(\(\?<(?![=!])(?<n>.*?)>)REGEX" );
			icu::RegexPattern* p = icu::RegexPattern::compile( up, 0, parse_error, status );
			if( U_FAILURE( status ) ) { errwr.WriteString( L"Internal error" ); return 11; }

			icu::RegexMatcher* m = icu_pattern->matcher( pattern, status );
			if( U_FAILURE( status ) ) { errwr.WriteString( L"Internal error" ); return 11; }

			for( ;; )
			{
				if( !m->find( status ) )
				{
					if( U_FAILURE( status ) ) { errwr.WriteString( L"Internal error" ); return 11; }

					break;
				}

				int32_t start = m->start( status );
				if( U_FAILURE( status ) ) { errwr.WriteString( L"Internal error" ); return 11; }

				int32_t end = m->end( status );
				if( U_FAILURE( status ) ) { errwr.WriteString( L"Internal error" ); return 11; }

				icu::UnicodeString possible_name = pattern.tempSubString( start, end - start );

				int group_number = p->groupNumberFromName( possible_name, status );
				// TODO: detect and show errors
				if( !U_FAILURE( status ) )
				{

				}

				//UnicodeString n( up.)
			}

		}

		icu::RegexMatcher* icu_matcher = icu_pattern->matcher( pattern, status );

		if( !Check( herr, status ) ) return 10;


		//..............
		// TODO: implement
		//icu_matcher->setTimeLimit( )

		bool is_not_first = false;

		for( ;; is_not_first = true )
		{
			if( !icu_matcher->find( status ) )
			{
				if( !Check( herr, status ) ) return 10;

				break;
			}

			int32_t start = icu_matcher->start( status );
			if( !Check( herr, status ) ) return 10;

			int32_t end = icu_matcher->end( status );
			if( !Check( herr, status ) ) return 10;

			if( is_not_first ) outwr.WriteString( L", " );

			outwr.WriteString256( L"[ [%i, %i]", start, end );

			int32_t group_count = icu_matcher->groupCount( );

			for( int32_t gr = 1; gr <= group_count; ++gr )
			{
				int32_t group_start = icu_matcher->start( gr, status );
				if( !Check( herr, status ) ) return 10;

				int32_t group_end = 0;

				if( group_start >= 0 )
				{
					group_end = icu_matcher->end( gr, status );
					if( !Check( herr, status ) ) return 10;
				}

				outwr.WriteString256( L", [%i, %i]", group_start, group_end );
			}

			outwr.WriteString( L" ]" );
		}
	}

	return 0;
}

