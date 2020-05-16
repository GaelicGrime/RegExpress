#pragma once

#include <vector>

#define _REGEX_MAX_STACK_COUNT      StdRegexInterop::Variable_REGEX_MAX_STACK_COUNT
#define _REGEX_MAX_COMPLEXITY_COUNT StdRegexInterop::Variable_REGEX_MAX_COMPLEXITY_COUNT
#include <regex>


namespace StdRegexInterop
{

	struct NativeMatch
	{
		enum TypeEnum : char { M = 'M', G = 'G' }; // 'M' -- match, 'G' -- group

		TypeEnum Type;
		ptrdiff_t Index; // (-1 for failed groups)
		ptrdiff_t Length;
	};


	extern thread_local long Variable_REGEX_MAX_STACK_COUNT;
	extern thread_local long Variable_REGEX_MAX_COMPLEXITY_COUNT;
	extern long Default_REGEX_MAX_STACK_COUNT;
	extern long Default_REGEX_MAX_COMPLEXITY_COUNT;


	void NativeMatches( std::vector<NativeMatch>* matches, std::string* error, std::wregex& regex, const std::wstring& text, std::regex_constants::match_flag_type flags,
		long aREGEX_MAX_STACK_COUNT, long aREGEX_MAX_COMPLEXITY_COUNT, HANDLE cancelEvent );

}
