#include "pch.h"

#include "Capture.h"
#include "Group.h"
#include "Match.h"
#include "Matcher.h"


using namespace System::Diagnostics;
using namespace System::Collections::Generic;
using namespace System::Text::RegularExpressions;

using namespace msclr::interop;
using namespace std;


namespace IcuRegexInterop
{

	static void Check( UErrorCode status );

	static Matcher::Matcher( )
	{
		mRegexGroupNames = gcnew Regex(
			R"REGEX( \(\?<(?![=!])(?'n'.*?)> )REGEX",
			RegexOptions::Compiled | RegexOptions::ExplicitCapture | RegexOptions::IgnorePatternWhitespace
		);

		BuildOptions( );
	}


	Matcher::Matcher( String^ pattern0, cli::array<String^>^ options )
		:mData( nullptr )
	{
		try
		{
			marshal_context context{};

			uint32_t icu_options = 0;

			for each( auto optdef in mOptions )
			{
				if( Array::IndexOf( options, optdef->FlagName ) >= 0 ) icu_options |= optdef->Flag;
			}

			wstring pattern = context.marshal_as<wstring>( pattern0 );

			UErrorCode status = U_ZERO_ERROR;
			UParseError parse_error{};

			icu::RegexPattern* icu_pattern =
				icu::RegexPattern::compile( icu::UnicodeString( (char16_t*)pattern.c_str( ), pattern.length( ) ),
					icu_options, parse_error, status );

			/*
				NOTE. Conversion from 'wchar_t*' to 'char16_t*' is required to work around a strange linker error:

				LNK2030	metadata inconsistent with COFF symbol table: symbol '??0UnicodeString@icu_65@@$$FQEAA@PEB_WH@Z' (0A0000AF) is different from '??0UnicodeString@icu_65@@$$FQEAA@PEB_SH@Z' in metadata
			*/

			if( U_FAILURE( status ) )
			{
				String^ error_name = gcnew String( u_errorName( status ) );

				throw gcnew Exception( String::Format( "Invalid pattern at line {0}, column {1}.\r\n\r\nError {2} ({3})", parse_error.line, parse_error.offset, error_name, (unsigned)status ) );
			}


			// try identifying group names

			mGroupNames = gcnew cli::array<String^>( 0 );

			auto matches = mRegexGroupNames->Matches( pattern0 );
			if( matches->Count > 0 )
			{
				for( int i = 0; i < matches->Count; ++i )
				{
					auto n = matches[i]->Groups["n"];
					if( n->Success )
					{
						String^ group_name = n->Value;
						wstring native_group_name = context.marshal_as<wstring>( group_name );

						int group_number = icu_pattern->groupNumberFromName( icu::UnicodeString( (char16_t*)native_group_name.c_str( ), native_group_name.length( ) ), status );

						// TODO: detect and show errors

						if( !U_FAILURE( status ) )
						{
							if( group_number >= mGroupNames->Length )
							{
								Array::Resize( mGroupNames, group_number + 1 );
							}

							mGroupNames[group_number] = group_name;
						}
					}
				}
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
					String^ group_name = nullptr;

					if( gr < mGroupNames->Length )
					{
						group_name = mGroupNames[gr];
					}

					if( group_name == nullptr )
					{
						group_name = gr.ToString( System::Globalization::CultureInfo::InvariantCulture );
					}

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


	void Matcher::BuildOptions( )
	{
#define O(f, n) \
	list->Add(gcnew OptionInfo( URegexpFlag::##f, gcnew String(#f), gcnew String(n)));

		List<OptionInfo^>^ list = gcnew List<OptionInfo^>( );

		O( UREGEX_CANON_EQ, "Forces normalization of pattern and strings" );
		O( UREGEX_CASE_INSENSITIVE, "Enable case insensitive matching" );
		O( UREGEX_COMMENTS, "Allow white space and comments within patterns" );
		O( UREGEX_DOTALL, "If set, '.' matches line terminators, otherwise '.' matching stops at line end" );
		O( UREGEX_LITERAL, "If set, treat the entire pattern as a literal string" );
		O( UREGEX_MULTILINE, "Control behavior of '$' and '^'. If set, recognize line terminators within string, otherwise, match only at start and end of input string" );
		O( UREGEX_UNIX_LINES, "Unix-only line endings. When this mode is enabled, only \\u000a is recognized as a line ending in the behavior of ., ^, and $" );
		O( UREGEX_UWORD, "Unicode word boundaries. If set, uses the Unicode TR 29 definition of word boundaries" );
		O( UREGEX_ERROR_ON_UNKNOWN_ESCAPES, "Error on Unrecognized backslash escapes" );

		mOptions = list;
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

