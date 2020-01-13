#pragma once


using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;
using namespace RegexEngineInfrastructure::Matches;


namespace CppStdRegexInterop
{
	struct MatcherData
	{
		std::wstring mText;
		std::wregex mRegex;
		std::regex_constants::match_flag_type mMatchFlags;
	};


	public ref class CppMatcher : IMatcher
	{
	public:

		CppMatcher( String^ pattern, cli::array<String^>^ options );

		~CppMatcher( );
		!CppMatcher( );


#pragma region IMatcher

		virtual RegexMatches^ Matches( String^ text );

#pragma endregion IMatcher


		const MatcherData* GetData( ) { return mData; }

	private:

		MatcherData* mData;
	};

}
