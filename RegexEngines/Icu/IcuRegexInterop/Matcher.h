#pragma once

#pragma unmanaged
#include <unicode/regex.h>
#pragma managed


using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;
using namespace RegexEngineInfrastructure::Matches;


namespace IcuRegexInterop
{
	struct MatcherData
	{
		icu::RegexPattern* mIcuRegexPattern;

		MatcherData( )
			:
			mIcuRegexPattern( nullptr )
		{

		}

		~MatcherData( )
		{
			delete mIcuRegexPattern;
			mIcuRegexPattern = nullptr;
		}
	};


	public ref class Matcher : IMatcher
	{
	public:

		static Matcher( );

		Matcher( String^ pattern, cli::array<String^>^ options );

		~Matcher( );
		!Matcher( );


		static String^ GetVersion( );


#pragma region IMatcher

		virtual RegexMatches^ Matches( String^ text );

#pragma endregion IMatcher


		String^ OriginalText; // TODO: make it read-only

	private:

		MatcherData* mData;
	};

}

