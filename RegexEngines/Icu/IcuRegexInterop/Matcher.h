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
	public ref class OptionInfo
	{
	public:
		URegexpFlag const Flag;
		String^ const FlagName;
		String^ const Note;

		OptionInfo( URegexpFlag flag, String^ flagName, String^ note )
			:
			Flag( flag ),
			FlagName( flagName ),
			Note( note )
		{
		}
	};


	struct MatcherData
	{
		icu::RegexPattern* mIcuRegexPattern;
		int32_t mTimeLimit;

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
		static List<OptionInfo^>^ GetOptions( ) { return mOptions; }


#pragma region IMatcher

		virtual RegexMatches^ Matches( String^ text );

#pragma endregion IMatcher


		String^ OriginalText; // TODO: make it read-only

	private:

		MatcherData* mData;
		cli::array<String^> ^ mGroupNames;

		static List<OptionInfo^>^ mOptions;
		static System::Text::RegularExpressions::Regex^ mRegexGroupNames;
		static String^ LimitPrefix;

		static void BuildOptions( );
	};

}

