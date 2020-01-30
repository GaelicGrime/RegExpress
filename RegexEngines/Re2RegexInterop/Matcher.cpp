#include "pch.h"

#include "Matcher.h"
#include "Match.h"


using namespace System::Diagnostics;

using namespace msclr::interop;


namespace Re2RegexInterop
{

	static Matcher::Matcher( )
	{
		mEmptyEnumeration = gcnew List<IMatch^>( 0 );

		BuildOptions( );
	}


	Matcher::Matcher( String^ pattern0, cli::array<String^>^ options )
		: mData( nullptr )
	{
		try
		{
			marshal_context context{};

			const wchar_t* pattern = context.marshal_as<const wchar_t*>( pattern0 );


			mData = new MatcherData{};

		}
		catch( const std::exception & exc )
		{
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( "Error: " + what );
		}
		catch( Exception ^ exc )
		{
			throw exc;
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


	String^ Matcher::GetRe2Version( )
	{
		return L"2020-01-01";
	}


	RegexMatches^ Matcher::Matches( String^ text0 )
	{
		// TODO: re-implement as lazy enumerator?

		try
		{
			auto matches = gcnew List<IMatch^>( );

			marshal_context context{};

			mData->mText = context.marshal_as<std::wstring>( text0 );

			//.......

			return gcnew RegexMatches( matches->Count, matches );
		}
		catch( const std::exception & exc )
		{
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( "Error: " + what );
		}
		catch( Exception ^ exc )
		{
			throw exc;
		}
		catch( ... )
		{
			// TODO: also catch 'boost::exception'?
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}


	void Matcher::BuildOptions( )
	{
#define C(f, n) \
	list->Add(gcnew OptionInfo( f, gcnew String(#f), gcnew String(n)));

		List<OptionInfo^>^ list = gcnew List<OptionInfo^>( );

		//C( PCRE2_ANCHORED, "Force pattern anchoring" );

		//mCompileOptions = list;


#undef C
	}
}
