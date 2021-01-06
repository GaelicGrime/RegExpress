#include "pch.h"
#include "BinaryReader.h"


unsigned __int8 BinaryReader::ReadByte( ) const
{
	unsigned __int8 b;
	DWORD n;

	if( !ReadFile( mHandle, &b, sizeof( b ), &n, NULL ) || n != sizeof( b ) )
	{
		throw L"Cannot read byte";
	}

	return b;
}


__int32 BinaryReader::ReadInt32( ) const
{
	__int32 i;

	ReadBytes( &i, sizeof( i ) );

	return i;
}


std::wstring BinaryReader::ReadString( ) const
{
	auto bytelen = Read7BitEncodedInt( );
	std::wstring s;

	if( ( bytelen % sizeof( s[0] ) ) != 0 )
	{
		throw L"Invalid odd string length";
	}

	s.resize( bytelen / sizeof( s[0] ) );

	ReadBytes( &s.front( ), bytelen );

	return s;
}


void BinaryReader::ReadBytes( void* buffer0, size_t size ) const
{
	char* dest = (char*)buffer0;
	size_t to_read = size;

	for( ;;)
	{
		DWORD n;
		if( !ReadFile( mHandle, dest, to_read, &n, NULL ) )
		{
			throw L"Failed to read bytes (1)";
		}

		if( n == 0 && to_read != 0 )
		{
			throw L"Failed to read bytes (2)";
		}

		to_read -= n;

		if( to_read == 0 ) break;

		dest += n;
	}
}


int BinaryReader::Read7BitEncodedInt( ) const
{
	// From the sources of .NET: https://referencesource.microsoft.com/#mscorlib/system/io/binaryreader.cs,f30b8b6e8ca06e0f

	// Read out an Int32 7 bits at a time.  The high bit
	// of the byte when on means to continue reading more bytes.
	int count = 0;
	int shift = 0;
	unsigned __int8 b;
	do
	{
		// Check for a corrupted stream.  Read a max of 5 bytes.
		// In a future version, add a DataFormatException.
		if( shift == 5 * 7 )  // 5 bytes max per Int32, shift += 7
			throw L"Format_Bad7BitInt32";

		// ReadByte handles end of stream cases for us.
		b = ReadByte( );
		count |= ( b & 0x7F ) << shift;
		shift += 7;
	} while( ( b & 0x80 ) != 0 );

	return count;
}
