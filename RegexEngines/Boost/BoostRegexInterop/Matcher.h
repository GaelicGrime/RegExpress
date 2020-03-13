#pragma once


using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;
using namespace RegexEngineInfrastructure::Matches;
using namespace RegexEngineInfrastructure::Matches::Simple;


namespace BoostRegexInterop
{
	public ref class OptionInfo
	{
	public:
		int const Flag;
		String^ const FlagName;
		String^ const Note;

		OptionInfo( int flag, String^ flagName, String^ note )
			:
			Flag( flag ),
			FlagName( flagName ),
			Note( note )
		{
		}
	};


	struct MatcherData
	{
		std::wstring mText;
		boost::wregex mRegex;
		boost::regex_constants::match_flag_type mMatchFlags;
	};


	public ref class Matcher : IMatcher, ISimpleTextGetter
	{
	public:

		static Matcher( );

		Matcher( String^ pattern, cli::array<String^>^ options );

		~Matcher( );
		!Matcher( );


		static String^ GetBoostVersion( );

		static List<OptionInfo^>^ GetCompileOptions( ) { return mCompileOptions; }
		static List<OptionInfo^>^ GetMatchOptions( ) { return mMatchOptions; }

#pragma region IMatcher

		virtual RegexMatches^ Matches( String^ text );

#pragma endregion

#pragma region ISimpleTextReader

		virtual String^ GetText( int index, int length );

#pragma endregion

	internal:

		const MatcherData* GetData( ) { return mData; }

		property System::Collections::Specialized::StringCollection^ GroupNames
		{
			System::Collections::Specialized::StringCollection^ get( )
			{
				return mGroupNames;
			}
		}

	private:

		MatcherData* mData;
		System::Collections::Specialized::StringCollection^ mGroupNames;

		static List<OptionInfo^>^ mCompileOptions;
		static List<OptionInfo^>^ mMatchOptions;
		static System::Text::RegularExpressions::Regex^ mRegexGroupNames;

		IMatch^ CreateMatch( const boost::wcmatch& match, Dictionary<int, String^>^ names );
		static void BuildOptions( );
	};

}
