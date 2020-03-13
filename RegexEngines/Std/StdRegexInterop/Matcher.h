#pragma once


using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;
using namespace RegexEngineInfrastructure::Matches;
using namespace RegexEngineInfrastructure::Matches::Simple;


namespace StdRegexInterop
{
	struct MatcherData
	{
		std::wstring mText;
		std::wregex mRegex;
		std::regex_constants::match_flag_type mMatchFlags;
	};


	public ref class Matcher : IMatcher, ISimpleTextGetter
	{
	public:

		Matcher( String^ pattern, cli::array<String^>^ options );

		~Matcher( );
		!Matcher( );


		static String^ GetCRTVersion( );


#pragma region IMatcher

		virtual RegexMatches^ Matches( String^ text );

#pragma endregion

#pragma region ISimpleTextGetter

		virtual String^ GetText( int index, int length );

#pragma endregion



		const MatcherData* GetData( ) { return mData; }

	private:

		IMatch^ CreateMatch( const std::wcmatch& match );

		MatcherData* mData;
	};

}
