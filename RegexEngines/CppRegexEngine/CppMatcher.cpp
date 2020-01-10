#include "pch.h"

#include "CppMatcher.h"
#include "CppRegexOptionInfo.h"
#include "CppMatch.h"


using namespace System::Diagnostics;

using namespace std;
using namespace msclr::interop;


namespace CppRegexEngine
{

	CppMatcher::CppMatcher( String^ pattern0, IReadOnlyCollection<IRegexOptionInfo^>^ options )
		: mData( nullptr )
	{
		marshal_context context{};
		wregex::flag_type flags{};

		wstring pattern = context.marshal_as<wstring>( pattern0 );

		for each( CppRegexOptionInfo ^ o in options )
		{
			flags |= o->Flag;
		}

		mData = new MatcherData{};

		mData->mRegex = wregex( pattern, flags );
	}


	CppMatcher::~CppMatcher( )
	{
		this->!CppMatcher( );
	}


	CppMatcher::!CppMatcher( )
	{
		delete mData;
		mData = nullptr;
	}


	RegexMatches^ CppMatcher::Matches( String^ text0 )
	{
		// TODO: re-implement as lazy enumerator?

		marshal_context context{};

		mData->mText = context.marshal_as<wstring>( text0 );

		auto matches = gcnew List<IMatch^>( );

		auto* native_text = mData->mText.c_str( );

		wcregex_iterator results_begin( native_text, native_text + mData->mText.length(), mData->mRegex, regex_constants::match_flag_type::match_default );
		wcregex_iterator results_end{};

		for( auto i = results_begin; i != results_end; ++i )
		{
			const wcmatch& match = *i;

			auto m = gcnew CppMatch( this, match );

			matches->Add( m );
		}

		return gcnew RegexMatches( matches->Count, matches );
	}

}
