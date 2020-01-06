#pragma once

#include <regex>

using namespace System;
using namespace System::Collections::Generic;
using namespace System::Linq;

using namespace RegexEngineInfrastructure::Matches;


namespace CppRegexEngine
{

	ref class CppGroup : public IGroup
	{
	public:

		CppGroup( int index, std::wssub_match submatch )
		{
			mSuccess = submatch.matched;
			mIndex = index;
			mValue = gcnew String( submatch.str( ).c_str( ) );
		}

#pragma region ICapture

		virtual property int Index
		{
			int get( )
			{
				return mIndex;
			}
		}

		virtual property int Length
		{
			int get( )
			{
				return mValue->Length;
			}
		}

		virtual property String^ Value
		{
			String^ get( )
			{
				return mValue;
			}
		}

#pragma endregion ICapture

#pragma region IGroup

		virtual property bool Success
		{
			bool get( )
			{
				return mSuccess;
			}
		}

		virtual property String^ Name
		{
			String^ get( )
			{
				return L""; //...?
			}
		}

		virtual property IEnumerable<ICapture^>^ Captures
		{
			IEnumerable<ICapture^>^ get( )
			{
				//...........
				// TODO: implement.
				return Enumerable::Empty<ICapture^>( );
			}
		}

#pragma endregion IGroup

	private:

		bool mSuccess;
		int mIndex;
		String^ mValue;

	};
}
