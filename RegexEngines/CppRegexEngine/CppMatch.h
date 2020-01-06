#pragma once

#include <regex>

using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure::Matches;


namespace CppRegexEngine
{

	ref class CppMatch : IMatch
	{

	public:

		CppMatch( std::wsmatch &&match )
		{

		}


#pragma region IGroup

		virtual property int Index
		{
			int get( ) 
			{
				return 0;
			}
		}

		virtual property int Length
		{
			int get( )
			{
				return 0;
			}
		}

		virtual property String^ Value
		{
			String^ get( )
			{
				return L"";
			}
		}

#pragma endregion IGroup

#pragma region IMatch
		
		virtual property bool Success
		{
			bool get( ) 
			{
				return false;
			}
		}

		virtual property String^ Name
		{
			String^ get( ) 
			{
				return L"";
			}
		}

		virtual property System::Collections::Generic::IEnumerable<RegexEngineInfrastructure::Matches::ICapture^>^ Captures;
		virtual property System::Collections::Generic::IEnumerable<RegexEngineInfrastructure::Matches::IGroup^>^ Groups;

#pragma endregion IGroup

	private:

		int mIndex;
		String^ mValue;

	};

}
