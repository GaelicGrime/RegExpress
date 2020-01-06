#pragma once


using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;
using namespace RegexEngineInfrastructure::Matches;


namespace CppRegexEngine
{

	ref class CppParsedPattern : IParsedPattern
	{
	public:

		CppParsedPattern( String^ pattern, IReadOnlyCollection<IRegexOptionInfo^>^ options );

		!CppParsedPattern( );


#pragma region IParsedPattern

		virtual RegexMatches^ Matches( String^ text );

#pragma endregion

	private:

		std::wregex * mRegex;
	};

}
