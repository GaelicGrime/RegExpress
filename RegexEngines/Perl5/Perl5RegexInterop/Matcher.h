#pragma once

using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure;
using namespace RegexEngineInfrastructure::Matches;
using namespace RegexEngineInfrastructure::Matches::Simple;


namespace Perl5RegexInterop
{

	public ref class Matcher : IMatcher, ISimpleTextGetter
	{
	public:

		static Matcher( );

		Matcher( String^ pattern, cli::array<String^>^ options );

		~Matcher( );
		!Matcher( );

		static String^ GetVersion( );

#pragma region IMatcher

		virtual RegexMatches^ Matches( String^ text );

#pragma endregion

#pragma region ISimpleTextReader

		virtual String^ GetText( int index, int length );

#pragma endregion

	private:

		String^ const Pattern;
		String^ OriginalText;
		static String^ volatile PerlVersion;
		static Object^ PerlLocker;

	};

}

