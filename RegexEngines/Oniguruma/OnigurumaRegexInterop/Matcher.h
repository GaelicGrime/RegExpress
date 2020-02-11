#pragma once


using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;
using namespace RegexEngineInfrastructure::Matches;


namespace OnigurumaRegexInterop
{
	public ref class OptionInfo
	{
	public:
		String^ const FlagName;
		String^ const Note;

		OptionInfo( String^ flagName, String^ note )
			:
			FlagName( flagName ),
			Note( note )
		{
		}
	};


	struct MatcherData
	{
		regex_t* mRegex;
		decltype( ONIG_OPTION_NONE ) mSearchOptions;

		MatcherData( )
			:
			mRegex( nullptr ),
			mSearchOptions( ONIG_OPTION_NONE )
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
		static List<OptionInfo^>^ GetSyntaxOptions( ) { return mSyntaxOptions; }
		static List<OptionInfo^>^ GetCompileOptions( ) { return mCompileOptions; }
		static List<OptionInfo^>^ GetSearchOptions( ) { return mSearchOptions; }

#pragma region IMatcher

		virtual RegexMatches^ Matches( String^ text );

#pragma endregion IMatcher

		String^ OriginalText; // TODO: make it read-only

	private:

		MatcherData* mData;

		static List<OptionInfo^>^ mSyntaxOptions;
		static List<OptionInfo^>^ mCompileOptions;
		static List<OptionInfo^>^ mSearchOptions;
		static Dictionary<String^, IntPtr>^ mTagToOption;

		static void BuildOptions( );
	};

}