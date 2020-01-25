#pragma once

using namespace System;
using namespace System::Collections::Generic;
using namespace System::Linq;

using namespace RegexEngineInfrastructure::Matches;


namespace CppBoostRegexInterop
{
	ref class CppMatch;


	ref class CppGroup : public IGroup
	{
	public:

		CppGroup( CppMatch^ parent, int groupNumber, int index, const boost::wcsub_match& submatch );


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
				return mLength;
			}
		}

		virtual property String^ Value
		{
			String^ get();
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
				return mGroupNumber.ToString(); // TODO: culture?
			}
		}

		virtual property IEnumerable<ICapture^>^ Captures
		{
			IEnumerable<ICapture^>^ get( )
			{
				return mCaptures;
			}
		}

#pragma endregion IGroup


		property CppMatch^ Parent
		{
			CppMatch^ get( )
			{
				return mParent;
			}
		}


	private:

		CppMatch^ const mParent;

		bool const mSuccess;
		int const mIndex;
		int const mLength;
		int const mGroupNumber;

		IList<ICapture^>^ mCaptures;
	};
}
