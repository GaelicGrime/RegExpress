#pragma once


using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;
using namespace RegexEngineInfrastructure::Matches;


namespace BoostRegexInterop
{
	struct MatcherData
	{
		std::wstring mText;
		boost::wregex mRegex;
		boost::regex_constants::match_flag_type mMatchFlags;
	};


	public ref class Matcher : IMatcher
	{
	public:

		Matcher( String^ pattern, cli::array<String^>^ options );

		~Matcher( );
		!Matcher( );


		static String^ GetBoostVersion( );


#pragma region IMatcher

		virtual RegexMatches^ Matches( String^ text );

#pragma endregion IMatcher


		const MatcherData* GetData( ) { return mData; }

	private:

		MatcherData* mData;
	};

}
