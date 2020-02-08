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
		String^ const FlagName;
		String^ const Note;
		bool const DefaultValue;

		OptionInfo( String^ flagName, String^ note, bool defaultValue )
			:
			FlagName( flagName ),
			Note( note ),
			DefaultValue( defaultValue )
		{
		}
	};


	struct MatcherData
	{
		std::unique_ptr<RE2> mRe;
		RE2::Anchor mAnchor;

		MatcherData( )
		{
			mAnchor = RE2::Anchor::UNANCHORED;
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

		static List<OptionInfo^>^ GetOptions( ) { return mOptions; }


#pragma region IMatcher

		virtual RegexMatches^ Matches( String^ text );

#pragma endregion IMatcher


		const MatcherData* GetData( ) { return mData; }

		String^ OriginalText; // TODO: make it read-only

	private:

		MatcherData* mData;

		static List<OptionInfo^>^ mOptions;

		static IEnumerable<IMatch^>^ mEmptyEnumeration;

		static void BuildOptions( );
	};
}
