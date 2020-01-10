#pragma once


using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;
using namespace RegexEngineInfrastructure::Matches;


namespace CppRegexEngine
{
	struct MatcherData
	{
		std::wstring mText;
		std::wregex mRegex;
	};


	ref class CppMatcher : IMatcher
	{
	public:

		CppMatcher( String^ pattern, IReadOnlyCollection<IRegexOptionInfo^>^ options );

		~CppMatcher( );
		!CppMatcher( );


#pragma region IParsedPattern

		virtual RegexMatches^ Matches( String^ text );

#pragma endregion

		const MatcherData* GetData( ) { return mData; }

	private:

		MatcherData* mData;
	};

}
