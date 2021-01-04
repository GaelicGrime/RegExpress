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


	void WriteString( const char* buffer, size_t size ) const;

	void WriteString( LPCWSTR text ) const;

	void __cdecl WriteString256( LPCWSTR format, ... ) const; //..........

private:

	HANDLE const mHandle;
};
