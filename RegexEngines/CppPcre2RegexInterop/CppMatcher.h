#pragma once


using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;
using namespace RegexEngineInfrastructure::Matches;


namespace CppPcre2RegexInterop
{
	struct MatcherData
	{
		std::wstring mText;
		pcre2_code* mRe;
		pcre2_match_data* mMatchData;

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


	public ref class CppMatcher : IMatcher
	{
	public:

		CppMatcher( String^ pattern, cli::array<String^>^ options );

		~CppMatcher( );
		!CppMatcher( );


		static String^ GetPcre2Version( );


#pragma region IMatcher

		virtual RegexMatches^ Matches( String^ text );

#pragma endregion IMatcher


		const MatcherData* GetData( ) { return mData; }

	private:

		MatcherData* mData;
	};

}
