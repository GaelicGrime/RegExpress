#pragma once


/// <summary>
/// A reader that is designed to be partially compatible with 'BinaryReader' class from .NET, using Unicode encoding.
/// </summary>
class BinaryReader final
{
public:

	BinaryReader( HANDLE h )
		: mHandle( h )
	{

	}


	unsigned __int8 ReadByte( ) const;

	__int32 ReadInt( ) const;

	std::wstring ReadString( ) const;

private:

	HANDLE const mHandle;

	int Read7BitEncodedInt( ) const;
};


