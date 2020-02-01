#pragma once


using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;
using namespace RegexEngineInfrastructure::Matches;


namespace Re2RegexInterop
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
		std::unique_ptr<RE2> mRe;

		MatcherData( )
		{
		}

		~MatcherData( )
		{
		}
	};


	public ref class Matcher : IMatcher
	{
	public:

		static Matcher( );

		Matcher( String^ pattern, cli::array<String^>^ options );

		~Matcher( );
		!Matcher( );


		static String^ GetRe2Version( );


#pragma region IMatcher

		virtual RegexMatches^ Matches( String^ text );

#pragma endregion IMatcher


		const MatcherData* GetData( ) { return mData; }

		String ^ OriginalText; // TODO: make it read-only

	private:

		MatcherData* mData;

		static IEnumerable<IMatch^>^ mEmptyEnumeration;

		static void BuildOptions( );
	};
}
