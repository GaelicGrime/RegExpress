#include "pch.h"

#include "Capture.h"
#include "Group.h"
#include "Match.h"
#include "Matcher.h"


using namespace System::Diagnostics;
using namespace System::Collections::Generic;

using namespace msclr::interop;
using namespace std;


namespace IcuRegexInterop
{

	static void Check( UErrorCode status );

	static Matcher::Matcher( )
	{
	}


	Matcher::Matcher( String^ pattern0, cli::array<String^>^ options )
		:mData( nullptr )
	{
		try
		{
			marshal_context context{};

			wstring pattern = context.marshal_as<wstring>( pattern0 );

			UErrorCode status = U_ZERO_ERROR;
			UParseError parse_error{};

			icu::RegexPattern* icu_pattern = icu::RegexPattern::compile( icu::UnicodeString( (char16_t*)pattern.c_str( ), pattern.length( ) ), 0, parse_error, status );

			/*
				NOTE. Conversion from 'wchar_t*' to 'char16_t*' is required to work around a strange linker error:

				LNK2030	metadata inconsistent with COFF symbol table: symbol '??0UnicodeString@icu_65@@$$FQEAA@PEB_WH@Z' (0A0000AF) is different from '??0UnicodeString@icu_65@@$$FQEAA@PEB_SH@Z' in metadata
			*/

			if( U_FAILURE( status ) )
			{
				String^ error_name = gcnew String( u_errorName( status ) );

				throw gcnew Exception( String::Format( "Invalid pattern at line {0}, column {1}.\r\n\r\nError {2} ({3})", parse_error.line, parse_error.offset, error_name, (unsigned)status ) );
			}

			mData = new MatcherData{};
			mData->mIcuRegexPattern = icu_pattern;

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


	RegexMatches^ Matcher::Matches( String^ text0 )
	{
		icu::RegexMatcher* icu_matcher = nullptr;

		try
		{
			OriginalText = text0;

			marshal_context context{};

			wstring text = context.marshal_as<wstring>( text0 );
			icu::UnicodeString unicode_string( (char16_t*)text.c_str( ), text.length( ) );

			UErrorCode status = U_ZERO_ERROR;
			icu_matcher = mData->mIcuRegexPattern->matcher( unicode_string, status );

			Check( status );

			List<IMatch^>^ matches = gcnew List<IMatch^>;

			for( ;;)
			{
				if( !icu_matcher->find( status ) )
				{
					Check( status );

					break;
				}

				int32_t start = icu_matcher->start( status );
				Check( status );

				int32_t end = icu_matcher->end( status );
				Check( status );

				Match^ match = gcnew Match( this, start, end - start );
				Group^ default_group = gcnew Group( match, "0", true, start, end - start );

				match->AddGroup( default_group );

				int32_t group_count = icu_matcher->groupCount( );
				for( int32_t gr = 1; gr <= group_count; ++gr )
				{
					int32_t group_start = icu_matcher->start( gr, status );
					Check( status );

					Group^ group;
					String^ group_name = gr.ToString( System::Globalization::CultureInfo::InvariantCulture );

					if( group_start < 0 )
					{
						group = gcnew Group( match, group_name, false, 0, 0 );
					}
					else
					{
						int32_t group_end = icu_matcher->end( gr, status );
						Check( status );

						group = gcnew Group( match, group_name, true, group_start, group_end - group_start );
					}

					match->AddGroup( group );
				}

				matches->Add( match );
			}

			return gcnew RegexMatches( matches->Count, matches );

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
		finally
		{
			delete icu_matcher;
		}
	}


	static void Check( UErrorCode status )
	{
		if( U_FAILURE( status ) )
		{
			String^ error_name = gcnew String( u_errorName( status ) );

			throw gcnew Exception( String::Format( "Error {0} ({1})", error_name, (unsigned)status ) );
		}
	}

}

