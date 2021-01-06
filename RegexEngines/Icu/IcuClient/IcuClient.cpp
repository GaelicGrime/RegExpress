// IcuClient.cpp : Defines the entry point for the application.
//

#include "pch.h"
#include "framework.h"
#include "IcuClient.h"

#include "BinaryReader.h"
#include "BinaryWriter.h"
#include "StreamWriter.h"


static void Check( UErrorCode status )
{
	if( U_FAILURE( status ) )
	{
		LPCSTR error_name = u_errorName( status );
		wchar_t buffer[256];

		StringCbPrintfW( buffer, sizeof( buffer ), L"Error %hs (%u)", error_name, (unsigned)status );

		throw buffer;
	}
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

	StreamWriter const errwr( herr );

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

	try
	{
		BinaryWriter outbw( hout );
		BinaryReader inbr( hin );

		std::wstring command = inbr.ReadString( );

		// 

		if( command == L"v" )
		{
			// get version

			auto v = L"" U_ICU_VERSION;

			outbw.Write( v );

			return 0;
		}

		//

		if( command == L"m" )
		{
			std::wstring pattern = inbr.ReadString( );
			std::wstring text = inbr.ReadString( );
			__int32 remote_flags = inbr.ReadInt32( );
			__int32 limit = inbr.ReadInt32( );

			uint32_t flags = 0;
			if( remote_flags & ( 1 << 0 ) ) flags |= UREGEX_CANON_EQ;
			if( remote_flags & ( 1 << 1 ) ) flags |= UREGEX_CASE_INSENSITIVE;
			if( remote_flags & ( 1 << 2 ) ) flags |= UREGEX_COMMENTS;
			if( remote_flags & ( 1 << 3 ) ) flags |= UREGEX_DOTALL;
			if( remote_flags & ( 1 << 4 ) ) flags |= UREGEX_LITERAL;
			if( remote_flags & ( 1 << 5 ) ) flags |= UREGEX_MULTILINE;
			if( remote_flags & ( 1 << 6 ) ) flags |= UREGEX_UNIX_LINES;
			if( remote_flags & ( 1 << 7 ) ) flags |= UREGEX_UWORD;
			if( remote_flags & ( 1 << 8 ) ) flags |= UREGEX_ERROR_ON_UNKNOWN_ESCAPES;

			UErrorCode status = U_ZERO_ERROR;
			UParseError parse_error{};

			icu::UnicodeString us_pattern( pattern.c_str( ), pattern.length( ) );

			icu::RegexPattern* icu_pattern = icu::RegexPattern::compile( us_pattern, flags, parse_error, status );

			if( U_FAILURE( status ) )
			{
				LPCSTR error_name = u_errorName( status );

				errwr.WriteStringF( L"Invalid pattern at line %i, column %i.\r\n\r\n(%hs, %u)",
					parse_error.line, parse_error.offset, error_name, (unsigned)status );

				return 9;
			}

			// try identifying named groups; (ICU does not seem to offer such feature)
			{
				icu::UnicodeString up( LR"REGEX(\(\?<(?![=!])(?<n>.*?)>)REGEX" );
				icu::RegexPattern* p = icu::RegexPattern::compile( up, 0, parse_error, status );
				if( U_FAILURE( status ) ) { errwr.WriteString( L"Internal error" ); return 11; }

				icu::RegexMatcher* m = p->matcher( us_pattern, status );
				if( U_FAILURE( status ) ) { errwr.WriteString( L"Internal error" ); return 11; }

				for( ;; )
				{
					status = U_ZERO_ERROR;

					if( !m->find( status ) )
					{
						if( U_FAILURE( status ) ) { errwr.WriteString( L"Internal error" ); return 11; }

						break;
					}

					int32_t start = m->start( 1, status );
					if( U_FAILURE( status ) ) { errwr.WriteString( L"Internal error" ); return 11; }

					int32_t end = m->end( 1, status );
					if( U_FAILURE( status ) ) { errwr.WriteString( L"Internal error" ); return 11; }

					icu::UnicodeString possible_name;
					us_pattern.extract( start, end - start, possible_name );
					if( U_FAILURE( status ) ) { errwr.WriteString( L"Internal error" ); return 11; }

					__int32 group_number = icu_pattern->groupNumberFromName( possible_name, status );
					// TODO: detect and show errors
					if( !U_FAILURE( status ) )
					{
						outbw.Write( group_number );
						outbw.Write( (LPCWSTR)possible_name.getBuffer( ), possible_name.length( ) );
					}
				}

				outbw.Write( (__int32)-1 ); // end of names
			}

			// find matches

			icu::UnicodeString us_text( text.c_str( ), text.length( ) );

			icu::RegexMatcher* icu_matcher = icu_pattern->matcher( us_text, status );
			Check( status );

			icu_matcher->setTimeLimit( limit, status );
			Check( status );

			for( ;; )
			{
				if( !icu_matcher->find( status ) )
				{
					Check( status );

					break;
				}

				int group_count = icu_matcher->groupCount( );

				outbw.Write( group_count );

				for( int i = 0; i <= group_count; ++i )
				{
					int32_t start = icu_matcher->start( i, status );
					Check( status );
					outbw.Write( start );

					if( start >= 0 )
					{
						int32_t end = icu_matcher->end( i, status );
						Check( status );
						outbw.Write( end );
					}
				}
			}

			outbw.Write( -1 );

			return 0;
		}

		errwr.WriteStringF( L"Unsupported command: '%s'", command.c_str( ) );

		return 1;
	}
	catch( LPCWSTR msg )
	{
		errwr.WriteString( msg );

		return 10;
	}
	catch( const std::exception& exc )
	{
		std::wstring m;
		for( const char* p = exc.what( ); *p != '\0'; ++p ) m.push_back( *p );

		errwr.WriteString( m.c_str( ) );

		return 11;
	}
	catch( ... )
	{
		errwr.WriteString( L"Internal error" );

		return 11;
	}

}

