#include "pch.h"
#include "Matcher.h"


using namespace System::Diagnostics;

using namespace msclr::interop;
using namespace std;


namespace IcuRegexInterop
{

	static Matcher::Matcher( )
	{
	}


	Matcher::Matcher( String^ pattern0, cli::array<String^>^ options )
		:mData( nullptr )
	{
		try
		{
			marshal_context context{};

			UErrorCode status = U_ZERO_ERROR;

			wstring pattern = context.marshal_as<wstring>( pattern0 );

			icu::RegexMatcher* icu_matcher = new icu::RegexMatcher( icu::UnicodeString( (char16_t*)pattern.c_str( ), pattern.length( ) ), 0, status );

			/*
				NOTE. Conversion from 'wchar_t*' to 'char16_t*' is required to work around a strange linker error:

				LNK2030	metadata inconsistent with COFF symbol table: symbol '??0UnicodeString@icu_65@@$$FQEAA@PEB_WH@Z' (0A0000AF) is different from '??0UnicodeString@icu_65@@$$FQEAA@PEB_SH@Z' in metadata
			*/

			mData = new MatcherData{};
			mData->mIcuRegexMatcher = icu_matcher;

		}
		catch( const std::exception & exc )
		{
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( "Error: " + what );
		}
		catch( Exception ^ exc )
		{
			throw;
		}
		catch( ... )
		{
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}


	Matcher::~Matcher( )
	{
		this->!Matcher( );
	}


	Matcher::!Matcher( )
	{
		delete mData;
		mData = nullptr;
	}


	String^ Matcher::GetVersion( )
	{
		return gcnew String( U_ICU_VERSION );
	}


	RegexMatches^ Matcher::Matches( String^ text )
	{
		try
		{
			OriginalText = text;


			return gcnew RegexMatches( 0, nullptr );

		}
		catch( const std::exception & exc )
		{
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( "Error: " + what );
		}
		catch( Exception ^ exc )
		{
			throw;
		}
		catch( ... )
		{
			// TODO: also catch 'boost::exception'?
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}

}

