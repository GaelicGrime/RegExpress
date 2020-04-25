#pragma once


using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;
using namespace RegexEngineInfrastructure::Matches;
using namespace RegexEngineInfrastructure::Matches::Simple;


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


	public ref class Matcher : IMatcher, ISimpleTextGetter
	{
	public:

		static Matcher( );

		Matcher( String^ pattern, cli::array<String^>^ options );

		~Matcher( );
		!Matcher( );


		static String^ GetRe2Version( );

		static List<OptionInfo^>^ GetOptions( ) { return mOptions; }


#pragma region IMatcher

		virtual RegexMatches^ Matches( String^ text, ICancellable^ cnc );

#pragma endregion

#pragma region ISimpleTextReader

		virtual String^ GetText( int index, int length );

#pragma endregion

	private:

		String^ OriginalText;
		MatcherData* mData;

		static List<OptionInfo^>^ mOptions;

		static IEnumerable<IMatch^>^ mEmptyEnumeration;

		IMatch^ CreateMatch( const std::vector<re2::StringPiece>& foundGroups, const std::map<int, std::string>& groupNames,
			const std::vector<char>& text, const std::vector<int>& indices );
		static void BuildOptions( );
	};
}
