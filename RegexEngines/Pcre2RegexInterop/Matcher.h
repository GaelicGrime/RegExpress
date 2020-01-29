#pragma once


using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;
using namespace RegexEngineInfrastructure::Matches;


namespace Pcre2RegexInterop
{

	struct MatcherData
	{
		std::wstring mText;
		pcre2_code* mRe;
		pcre2_match_data* mMatchData;
		int mMatcherOptions;

		MatcherData( )
		{
			mRe = nullptr;
			mMatchData = nullptr;
		}

		~MatcherData( )
		{
			if( mMatchData )
			{
				pcre2_match_data_free( mMatchData );
				mMatchData = nullptr;
			}

			if( mRe )
			{
				pcre2_code_free( mRe );
				mRe = nullptr;
			}
		}
	};


	public ref class OptionInfo
	{
	public:
		property int	 Flag;
		property String^ FlagName;
		property String^ Note;

		OptionInfo( int flag, String^ flagName, String^ note )
		{
			Flag = flag;
			FlagName = flagName;
			Note = note;
		}
	};


	public ref class Matcher : IMatcher
	{
	public:

		static Matcher( );

		Matcher( String^ pattern, cli::array<String^>^ options );

		~Matcher( );
		!Matcher( );


		static String^ GetPcre2Version( );

		static List<OptionInfo^>^ GetCompileOptions( ) { return mCompileOptions; }
		static List<OptionInfo^>^ GetMatchOptions( ) { return mMatchOptions; }


#pragma region IMatcher

		virtual RegexMatches^ Matches( String^ text );

#pragma endregion IMatcher


		const MatcherData* GetData( ) { return mData; }

	private:

		MatcherData* mData;

		static List<OptionInfo^>^ mCompileOptions;
		static List<OptionInfo^>^ mMatchOptions;

		static IEnumerable<IMatch^>^ mEmptyEnumeration;

		static void BuildOptions( );
	};

}
