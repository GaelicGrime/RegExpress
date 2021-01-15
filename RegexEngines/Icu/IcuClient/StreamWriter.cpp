#include "pch.h"
#include "StreamWriter.h"


void StreamWriter::WriteBytes( const void* buffer, size_t size ) const
{
	DWORD count;
	WriteFile( mHandle, buffer, size, &count, NULL );
}


void StreamWriter::WriteString( LPCWSTR text ) const
{
	WriteBytes( text, lstrlenW( text ) * sizeof( WCHAR ) );
}


void __cdecl StreamWriter::WriteStringF( LPCWSTR format, ... ) const
{
	wchar_t buffer[256];
	bool success = false;

	va_list argptr;
	va_start( argptr, format );

	HRESULT hr = StringCbVPrintfW( buffer, sizeof( buffer ), format, argptr );

	if( SUCCEEDED( hr ) )
	{
		WriteString( buffer );
	}
	else
	{
		int size = lstrlenW( format ) + 128; // in wchar
		const int step = 512; // in wchar

		wchar_t* dynbuff = nullptr;

		for( ;;)
		{
			if( hr != STRSAFE_E_INSUFFICIENT_BUFFER )
			{
				free( dynbuff );
				throw "Failed to write formatted string";
			}

			if( size >= STRSAFE_MAX_CCH - step )
			{
				free( dynbuff );
				throw "Too long formatted string";
			}

			size += step;
			wchar_t* newbuff = (wchar_t*)realloc( dynbuff, size * sizeof( format[0] ) );

			if( newbuff == 0 )
			{
				free( dynbuff );
				throw "Insufficient memory to format the string";
			}

			dynbuff = newbuff;

			hr = StringCchVPrintfW( dynbuff, size, format, argptr );

			if( SUCCEEDED( hr ) ) break;
		}

		WriteString( dynbuff );

		free( dynbuff );
	}

	va_end( argptr );
}


std::wstring StreamWriter::Printf( LPCWSTR format, ... )
{
	va_list argptr;
	va_start( argptr, format );

	int size = lstrlenW( format ) + 128; // in wchar
	const int step = 512; // in wchar
	HRESULT hr;

	std::wstring result;

	for( ;;)
	{
		result.resize( size );

		hr = StringCchVPrintfW( &result[0], result.size( ), format, argptr );

		if( SUCCEEDED( hr ) ) break;

		if( hr != STRSAFE_E_INSUFFICIENT_BUFFER )
		{
			throw "Failed to write formatted string";
		}

		if( size >= STRSAFE_MAX_CCH - step )
		{
			throw "Too long formatted string";
		}

		size += step;
	}

	size_t len;

	hr = StringCchLengthW( result.c_str( ), result.size( ), &len );

	if( !SUCCEEDED( hr ) )
	{
		throw "Failed to get formatted string length";
	}

	result.resize( len );
	result.shrink_to_fit( );

	va_end( argptr );

	return result;
}
