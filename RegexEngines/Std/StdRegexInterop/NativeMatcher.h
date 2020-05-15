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

	extern long Variable_REGEX_MAX_STACK_COUNT;
	extern long Variable_REGEX_MAX_COMPLEXITY_COUNT;

	// Not thread-safe because of shared 'Variable_REGEX_MAX_STACK_COUNT' and 'Variable_REGEX_MAX_COMPLEXITY_COUNT' 
	void NativeMatches( std::vector<NativeMatch>* matches, long aREGEX_MAX_STACK_COUNT, long aREGEX_MAX_COMPLEXITY_COUNT,
		const wchar_t* begin, const wchar_t* end, std::wregex& regex, std::regex_constants::match_flag_type flags,
		char* errorText, size_t errorTextSize );

}
