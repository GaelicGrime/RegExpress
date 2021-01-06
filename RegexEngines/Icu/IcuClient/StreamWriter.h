#pragma once


class StreamWriter
{
public:

	StreamWriter( HANDLE h )
		: mHandle( h )
	{
		assert( h != INVALID_HANDLE_VALUE );
		// (probably 0 is a valid handle)
	}


	void WriteBytes( const void* buffer, size_t size ) const;

	void WriteString( LPCWSTR text ) const;

	void __cdecl WriteStringF( LPCWSTR format, ... ) const;

	static std::wstring Printf( LPCWSTR format, ... );

private:

	HANDLE const mHandle;
};
