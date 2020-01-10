#pragma once

#include "CppRegexOptionInfo.h"
#include "CppMatcher.h"


using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;


namespace CppRegexEngine
{
	public ref class CppRegexEngine : public IRegexEngine
	{
	public:

#pragma region IRegexEngine

		virtual property String^ Id
		{
			String^ get( )
			{
				return L"CppRegex";
			}
		}


		virtual property IReadOnlyCollection<IRegexOptionInfo^>^ AllOptions
		{
			IReadOnlyCollection<IRegexOptionInfo^>^ get( ) 
			{
				auto list = gcnew List<IRegexOptionInfo^>;

#define ADD(flag, note) \
	list->Add( gcnew CppRegexOptionInfo( L#flag, note, L#flag, std::wregex::flag ) );

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
		}


		virtual IMatcher^ ParsePattern( String^ pattern, IReadOnlyCollection<IRegexOptionInfo^>^ options )
		{
			return gcnew CppMatcher( pattern, options );
		}

#pragma endregion IRegexEngine


	private:

	};
}
