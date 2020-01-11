#include "pch.h"

#include "CppRegexEngine.h"


using namespace std;


namespace CppRegexEngineNs
{

	IReadOnlyCollection<IRegexOptionInfo^>^ CppRegexEngine::AllOptions::get( )
	{
		auto list = gcnew List<IRegexOptionInfo^>;

#define ADD(flag, note) \
	list->Add( gcnew CppRegexSimpleOptionInfo( L#flag, note, L#flag, std::wregex::flag ) );

		ADD( ECMAScript, L"" );
		ADD( basic, L"" );
		ADD( extended, L"" );
		ADD( awk, L"" );
		ADD( grep, L"" );
		ADD( egrep, L"" );
		ADD( icase, L"" );
		ADD( nosubs, L"" );
		ADD( optimize, L"" );
		ADD( collate, L"" );
#undef ADD

		return list;
	}


	IMatcher^ CppRegexEngine::ParsePattern( String^ pattern, IReadOnlyCollection<IRegexSimpleOptionInfo^>^ options )
	{
		return gcnew CppMatcher( pattern, options );
	}

}
