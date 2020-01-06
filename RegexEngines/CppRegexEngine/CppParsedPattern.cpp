#include "pch.h"

#include "CppParsedPattern.h"
#include "CppRegexOptionInfo.h"
#include "CppMatch.h"


using namespace System::Diagnostics;

using namespace std;


namespace CppRegexEngine
{

	CppParsedPattern::CppParsedPattern( String^ pattern, IReadOnlyCollection<IRegexOptionInfo^>^ options )
		: mRegex( nullptr )
	{
		msclr::interop::marshal_context context;
		wregex::flag_type flags;

		wstring pattern2 = context.marshal_as<wstring>( pattern );

		//auto e = options->GetEnumerator( );
		//e->Reset( );
		//while( e-> )
		//for( int i = 0;  )

		for each( CppRegexOptionInfo ^ o in options )
		{
			flags |= o->Flag;
		}

		mRegex = new wregex( pattern2, flags );
	}


	CppParsedPattern::!CppParsedPattern( )
	{
		delete mRegex;
		mRegex = nullptr;
	}


	RegexMatches^ CppParsedPattern::Matches( String^ text0 )
	{
		// TODO: re-implement as lazy enumerator?

		msclr::interop::marshal_context context;

		wstring text = context.marshal_as<wstring>( text0 );

		auto results_begin = wsregex_iterator( text.cbegin( ), text.cend( ), *mRegex );
		auto results_end = wsregex_iterator( );

		auto matches = gcnew List<IMatch^>( );

		for( auto i = results_begin; i != results_end; ++i )
		{
			const wsmatch& match = *i;

			auto m = gcnew CppMatch( match );

			matches->Add( m );
		}

		return gcnew RegexMatches( matches->Count, matches );
	}

}
