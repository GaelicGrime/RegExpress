#pragma once

#include "CppRegexOptionInfo.h"

using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;
using namespace RegexEngineInfrastructure::Matches;


namespace CppRegexEngine
{
	enum class CppRegexOptions
	{
		ECMAScript = std::wregex::flag_type::ECMAScript,
		basic = std::wregex::flag_type::basic,
		extended = std::wregex::flag_type::extended,
		awk = std::wregex::flag_type::awk,
		grep = std::wregex::flag_type::grep,
		egrep = std::wregex::flag_type::egrep,
		//_Gmask = std::wregex::flag_type::_Gmask,

		icase = std::wregex::flag_type::icase,
		nosubs = std::wregex::flag_type::nosubs,
		optimize = std::wregex::flag_type::optimize,
		collate = std::wregex::flag_type::collate
	};


	public ref class CppRegexEngine : public IRegexEngine
	{
	public:

#pragma region IRegexEngine

		property String^ Id
		{
			String^ get( ) override
			{
				return L"CppRegex";
			}
		}


		property IReadOnlyCollection<IRegexOptionInfo^>^ AllOptions
		{
			IReadOnlyCollection<IRegexOptionInfo^>^ get( ) override
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


		IParsedPattern^ ParsePattern( String^ pattern, IReadOnlyCollection<IRegexOptionInfo^>^ options ) override
		{
			throw; //..........
			return nullptr;
		}

#pragma endregion IRegexEngine








		static void Matches( String^ text0, String^ pattern0, CppRegexOptions options0 );


	private:

	};
}
