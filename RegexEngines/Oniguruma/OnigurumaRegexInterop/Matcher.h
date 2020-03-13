#pragma once


using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;
using namespace RegexEngineInfrastructure::Matches;
using namespace RegexEngineInfrastructure::Matches::Simple;


namespace OnigurumaRegexInterop
{
	ref class OnigurumaHelper;

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


	public ref class Matcher : IMatcher, ISimpleTextGetter
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
		static List<OptionInfo^>^ GetConfigurationOptions( ) { return mConfigurationOptions; }

		static OnigurumaHelper^ CreateOnigurumaHelper( cli::array<String^>^ options );

#pragma region IMatcher

		virtual RegexMatches^ Matches( String^ text );

#pragma endregion

#pragma region ISimpleTextReader

		virtual String^ GetText( int index, int length );

#pragma endregion


		String^ OriginalText; // TODO: make it read-only

	private:

		MatcherData* mData;

		static List<OptionInfo^>^ mSyntaxOptions;
		static List<OptionInfo^>^ mCompileOptions;
		static List<OptionInfo^>^ mSearchOptions;
		static List<OptionInfo^>^ mConfigurationOptions;
		static Dictionary<String^, IntPtr>^ mTagToOption;

		IMatch^ CreateMatch( OnigRegion* region, List<String^>^ groupNames );
		static void BuildOptions( );
	};

}