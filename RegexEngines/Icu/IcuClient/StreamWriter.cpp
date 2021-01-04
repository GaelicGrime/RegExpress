#include "pch.h"
#include "StreamWriter.h"


void StreamWriter::WriteString( const char* buffer, size_t size ) const
{
	DWORD count;
	WriteFile( mHandle, buffer, size, &count, NULL );
}


void StreamWriter::WriteString( LPCWSTR text ) const
{
	WriteString( (const char*)text, lstrlenW( text ) * sizeof( WCHAR ) );
}


void __cdecl StreamWriter::WriteString256( LPCWSTR format, ... ) const
{
	wchar_t buffer[256];

	va_list argptr;
	va_start( argptr, format );

	StringCbVPrintfW( buffer, sizeof( buffer ), format, argptr );

	WriteString( buffer );

	va_end( argptr );
}
