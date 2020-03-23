#include "pch.h"
#include "Matcher.h"


using namespace System::Diagnostics;
using namespace System::Runtime::InteropServices;
using namespace msclr::interop;


namespace SubRegRegexInterop
{
	Matcher::Matcher( String^ pattern, cli::array<String^>^ options )
		: Pattern( pattern ), MaximumDepth( 4 )
	{
		String^ MaximumDepthPrefix = "depth:";

		for each( String ^ o in options )
		{
			if( o->StartsWith( MaximumDepthPrefix ) )
			{
				String^ maximum_depth_s = o->Substring( MaximumDepthPrefix->Length );
				int maximum_depth = 0;

				if( !int32_t::TryParse( maximum_depth_s, maximum_depth ) )
				{
					throw gcnew Exception( String::Format( "Invalid maximum depth: '{0}'. Enter a number between 0 and {1}", maximum_depth_s, INT_MAX ) );
				}

				MaximumDepth = maximum_depth;
			}
		}
	}


	Matcher::~Matcher( )
	{
		this->!Matcher( );
	}


	Matcher::!Matcher( )
	{
	}


	RegexMatches^ Matcher::Matches( String^ text0 )
	{
		try
		{
			OriginalText = text0;

			marshal_context context{};

			std::string pattern = context.marshal_as<std::string>( Pattern );
			std::string text = context.marshal_as<std::string>( text0 );

			const int MAX_CAPTURES = 1000;
			std::unique_ptr<subreg_capture_t[]> captures( new subreg_capture_t[MAX_CAPTURES]( ) );

			int result = subreg_match( pattern.c_str( ), text.c_str( ), captures.get( ), MAX_CAPTURES, MaximumDepth );

			CheckResult( result );

			auto matches = gcnew List<IMatch^>( );

			if( result > 0 )
			{
				const subreg_capture_t& capture0 = captures[0];
				int index0 = capture0.start - text.c_str( );
				int length0 = capture0.length;

				auto match = SimpleMatch::Create( index0, length0, this );

				for( int i = 0; i < result; ++i )
				{
					// (the first is the entire input)
					const subreg_capture_t& capture = captures[i];
					int index = capture.start - text.c_str( );
					int length = capture.length;

					match->AddGroup( index, length, true, i.ToString( System::Globalization::CultureInfo::InvariantCulture ) );
				}

				matches->Add( match );
			}



			return gcnew RegexMatches( matches->Count, matches );

		}
		catch( Exception ^ exc )
		{
			UNREFERENCED_PARAMETER( exc );
			throw;
		}
		catch( ... )
		{
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}


	String^ Matcher::GetText( int index, int length )
	{
		return OriginalText->Substring( index, length );
	}


	void Matcher::CheckResult( int result )
	{
		if( result >= 1 || result == SUBREG_RESULT_NO_MATCH ) return;

		const char* msg = nullptr;

#define T(code, message) \
	case code : msg = message; break;

		switch( result )
		{
			//T( SUBREG_RESULT_NO_MATCH, "No match occurred" )
			T( SUBREG_RESULT_INVALID_ARGUMENT, "Invalid argument passed to function." )
				T( SUBREG_RESULT_ILLEGAL_EXPRESSION, "Syntax error found in regular expression." )
				T( SUBREG_RESULT_MISSING_BRACKET, "A closing group bracket is missing from the regular expression." )
				T( SUBREG_RESULT_SURPLUS_BRACKET, "A closing group bracket without a matching opening group bracket has been found." )
				T( SUBREG_RESULT_INVALID_METACHARACTER, "The regular expression contains an invalid metacharacter (typically a malformed \\ escape sequence)" )
				T( SUBREG_RESULT_MAX_DEPTH_EXCEEDED, "The nesting depth of groups contained within the regular expression exceeds the limit specified by max_depth." )
				T( SUBREG_RESULT_CAPTURE_OVERFLOW, "Capture array not large enough." )
				T( SUBREG_RESULT_INVALID_OPTION, "Invalid inline option specified." )

		default:
			throw gcnew Exception( String::Format( "Unknown result: {0}", result ) );
			break;
		}

#undef T

		if( msg != nullptr )
		{
			throw gcnew Exception( gcnew String( msg ) );
		}
	}

}
