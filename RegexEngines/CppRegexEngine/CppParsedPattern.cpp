#include "pch.h"

#include "CppParsedPattern.h"
#include "CppRegexOptionInfo.h"


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
		msclr::interop::marshal_context context;

		wstring text = context.marshal_as<wstring>( text0 );

		auto results_begin = wsregex_iterator( text.cbegin( ), text.cend( ), *mRegex );
		auto results_end = wsregex_iterator( );

		//List<

		for( auto i = results_begin; i != results_end; ++i )
		{
			const wsmatch& match = *i;

			int Index = match.position();
			String^ Value = context.marshal_as<String^>( match.str( ) );// gcnew String( match.str( ).c_str( ) );

			/*

			auto submatches_begin = match.cbegin( );
			auto submatches_end = match.cend( );

			for( auto j = submatches_begin; j != submatches_end; ++j )
			{
				wssub_match submatch = *j;

				wstring value = submatch.str( );
				int position = submatch.first - text2.cbegin( );
				int length = submatch.length();

				Debug::Assert( value.length( ) == length );

				//submatch.

			}
			*/
		}

		return nullptr;
	}

}
