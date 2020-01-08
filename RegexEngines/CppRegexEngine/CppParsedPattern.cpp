#include "pch.h"

#include "CppParsedPattern.h"
#include "CppRegexOptionInfo.h"
#include "CppMatch.h"


using namespace System::Diagnostics;

using namespace std;


namespace CppRegexEngine
{

	CppParsedPattern::CppParsedPattern( String^ pattern0, IReadOnlyCollection<IRegexOptionInfo^>^ options )
		: mRegex( nullptr )
	{
		msclr::interop::marshal_context context;
		wregex::flag_type flags;

		wstring pattern = context.marshal_as<wstring>( pattern0 );

		//auto e = options->GetEnumerator( );
		//e->Reset( );
		//while( e-> )
		//for( int i = 0;  )

		for each( CppRegexOptionInfo ^ o in options )
		{
			flags |= o->Flag;
		}
		auto p = new wstring( pattern );
		mRegex = new wregex( *p, flags );
	}


	CppParsedPattern::!CppParsedPattern( )
	{
		//.........delete mRegex;
		//.........mRegex = nullptr;
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
