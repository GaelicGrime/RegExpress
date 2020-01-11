#pragma once

#include "CppRegexOptionInfo.h"
#include "CppMatcher.h"


using namespace System;
using namespace System::Collections::Generic;
using namespace System::Windows::Controls;

using namespace RegexEngineInfrastructure;


namespace CppRegexEngineNs
{
	public ref class CppRegexEngine : public IRegexEngine
	{
	public:

#pragma region IRegexEngine

		virtual property String^ Id
		{
			String^ get( )
			{
				return L"CppRegex";
			}
		}


		virtual UserControl^ GetOptionsUserControl( )
		{
			return gcnew CppRegexEngineControls::UCCppRegexOptions( );
		}


		virtual property IReadOnlyCollection<IRegexOptionInfo^>^ AllOptions
		{
			IReadOnlyCollection<IRegexOptionInfo^>^ get( );
		}


		virtual IMatcher^ ParsePattern( String^ pattern, IReadOnlyCollection<IRegexSimpleOptionInfo^>^ options );

#pragma endregion IRegexEngine


	private:

	};
}
