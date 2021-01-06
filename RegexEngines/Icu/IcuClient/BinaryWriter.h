#pragma once


/// <summary>
/// A writer that is designed to be partially compatible with 'BinaryWriter' class from .NET, using Unicode encoding.
/// </summary>
class BinaryWriter final
{
public:

	BinaryWriter( HANDLE h )
		: mHandle( h )
	{

	}


	void Write( unsigned __int8 b )
	{
		WriteBytes( &b, sizeof( b ) );
	}


	void Write( __int32 i )
	{
		WriteBytes( &i, sizeof( i ) );
	}


	void Write( LPCWSTR s );
	void Write( LPCWSTR s, int charlen );

	void WriteBytes( const void* buffer0, DWORD size );


private:

	HANDLE const mHandle;

	void Write7BitEncodedInt( int value );
};
