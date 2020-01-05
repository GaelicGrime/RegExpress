#pragma once

using namespace System;

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


	public ref class CppRegexEngine
	{
	public:

		static void Matches( String^ text0, String^ pattern0, CppRegexOptions options0 );
	};
}
