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
		long mREGEX_MAX_STACK_COUNT;
		long mREGEX_MAX_COMPLEXITY_COUNT;
	};


	public ref class Matcher : IMatcher, ISimpleTextGetter
	{
	public:

		static property String^ OptionPrefix_REGEX_MAX_STACK_COUNT
		{
			String^ get( )
			{
				return ConstOptionPrefix_REGEX_MAX_STACK_COUNT;
			}
		}

		static property String^ OptionPrefix_REGEX_MAX_COMPLEXITY_COUNT
		{
			String^ get( )
			{
				return ConstOptionPrefix_REGEX_MAX_COMPLEXITY_COUNT;
			}
		}

		static property long Default_REGEX_MAX_STACK_COUNT { long get( ); }
		static property long Default_REGEX_MAX_COMPLEXITY_COUNT { long get( ); }


		static Matcher( );
		Matcher( String^ pattern, cli::array<String^>^ options );

		~Matcher( );
		!Matcher( );


		static String^ GetCRTVersion( );


#pragma region IMatcher

		virtual RegexMatches^ Matches( String^ text, ICancellable^ cnc );

#pragma endregion

#pragma region ISimpleTextGetter

		virtual String^ GetText( int index, int length );

#pragma endregion

	private:

		MatcherData* mData;
		static String^ ConstOptionPrefix_REGEX_MAX_STACK_COUNT;
		static String^ ConstOptionPrefix_REGEX_MAX_COMPLEXITY_COUNT;
	};

}
