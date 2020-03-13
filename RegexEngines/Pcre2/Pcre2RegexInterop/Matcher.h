#pragma once


using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;
using namespace RegexEngineInfrastructure::Matches;
using namespace RegexEngineInfrastructure::Matches::Simple;


namespace Pcre2RegexInterop
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


	enum class Algorithm
	{
		Standard,
		DFA,
	};


	struct MatcherData
	{
		Algorithm mAlgorithm;
		std::wstring mText;

		pcre2_compile_context* mCompileContext;
		pcre2_code* mRe;
		pcre2_match_context* mMatchContext;
		pcre2_match_data* mMatchData;
		int mMatcherOptions;
		std::vector<int> mDfaWorkspace;

		MatcherData( )
		{
			mAlgorithm = Algorithm::Standard;
			mCompileContext = nullptr;
			mRe = nullptr;
			mMatchContext = nullptr;
			mMatchData = nullptr;
			mMatcherOptions = 0;
		}

		~MatcherData( )
		{
			if( mMatchContext )
			{
				pcre2_match_context_free( mMatchContext );
				mMatchContext = nullptr;
			}

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

			if( mCompileContext )
			{
				pcre2_compile_context_free( mCompileContext );
				mCompileContext = nullptr;
			}
		}
	};


	public ref class Matcher : IMatcher, ISimpleTextGetter
	{
	public:

		static Matcher( );

		Matcher( String^ pattern, cli::array<String^>^ options );

		~Matcher( );
		!Matcher( );


		static String^ GetPcre2Version( );

		static List<OptionInfo^>^ GetCompileOptions( ) { return mCompileOptions; }
		static List<OptionInfo^>^ GetExtraCompileOptions( ) { return mExtraCompileOptions; }
		static List<OptionInfo^>^ GetMatchOptions( ) { return mMatchOptions; }


#pragma region IMatcher

		virtual RegexMatches^ Matches( String^ text );

#pragma endregion

#pragma region ISimpleTextReader

		virtual String^ GetText( int index, int length );

#pragma endregion

	private:

		MatcherData* mData;

		static List<OptionInfo^>^ mCompileOptions;
		static List<OptionInfo^>^ mExtraCompileOptions;
		static List<OptionInfo^>^ mMatchOptions;

		static IEnumerable<IMatch^>^ mEmptyEnumeration;

		IMatch^ CreateMatch( pcre2_code* re, PCRE2_SIZE* ovector, int rc );
		static void BuildOptions( );
	};

}
