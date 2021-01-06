#include "pch.h"
#include "BinaryWriter.h"


void BinaryWriter::Write( LPCWSTR s )
{
	int charlen = lstrlenW( s );

	Write( s, charlen );
}


void BinaryWriter::Write( LPCWSTR s, int charlen )
{
	int bytelen = charlen * sizeof( s[0] );

	Write7BitEncodedInt( bytelen );
	WriteBytes( s, bytelen );
}


void BinaryWriter::WriteBytes( const void* buffer0, DWORD size )
{
	const char* buffer = (const char*)buffer0;
	DWORD to_write = size;
	DWORD written;

	for( ;;)
	{
		if( !WriteFile( mHandle, buffer, to_write, &written, NULL ) )
		{
			throw L"Failed to write bytes";
		}

		to_write -= written;

		assert( to_write >= 0 );

		if( to_write <= 0 ) break;

		buffer += written;
	}
}


void BinaryWriter::Write7BitEncodedInt( int value )
{
	// From the sources of .NET: https://referencesource.microsoft.com/#mscorlib/system/io/binarywriter.cs,cf806b417abe1a35

	// Write out an int 7 bits at a time.  The high bit of the byte,
	// when on, tells reader to continue reading more bytes.
	unsigned int v = (unsigned int)value;   // support negative numbers
	while( v >= 0x80 )
	{
		Write( (unsigned __int8)( v | 0x80 ) );
		v >>= 7;
	}

	Write( (unsigned __int8)v );
}
