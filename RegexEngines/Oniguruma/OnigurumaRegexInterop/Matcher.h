#pragma once


using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;
using namespace RegexEngineInfrastructure::Matches;


namespace OnigurumaRegexInterop
{
	struct MatcherData
	{
		regex_t* mRegex;

		MatcherData( )
			:mRegex( nullptr )
		{

		}

		~MatcherData( )
		{
			if( mRegex ) onig_free( mRegex );
			mRegex = nullptr;
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