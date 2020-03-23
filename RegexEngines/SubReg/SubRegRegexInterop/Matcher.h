#pragma once


using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;
using namespace RegexEngineInfrastructure::Matches;
using namespace RegexEngineInfrastructure::Matches::Simple;


namespace SubRegRegexInterop
{
	public ref class Matcher : IMatcher, ISimpleTextGetter
	{
	public:
		Matcher( String^ pattern, cli::array<String^>^ options );

		~Matcher( );
		!Matcher( );

#pragma region IMatcher

		virtual RegexMatches^ Matches( String^ text );

#pragma endregion

#pragma region ISimpleTextReader

		virtual String^ GetText( int index, int length );

#pragma endregion

	private:

		int MaximumDepth;
		String^ const Pattern;
		String^ OriginalText;

		void CheckResult( int result );

	};
}

