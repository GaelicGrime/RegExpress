#pragma once

#include <regex>

using namespace System;
using namespace System::Collections::Generic;
using namespace System::Linq;

using namespace RegexEngineInfrastructure::Matches;


namespace StdRegexInterop
{
	ref class Match;


	ref class Group : public IGroup
	{
	public:

		Group( Match^ parent, int groupNumber );
		Group( Match^ parent, int groupNumber, int index, const std::wcsub_match& submatch );


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
			String^ get( );
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
				return mGroupNumber.ToString( System::Globalization::CultureInfo::InvariantCulture );
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

	private:

		Match^ const mParent;

		bool const mSuccess;
		int const mIndex;
		int const mLength;
		int const mGroupNumber;

		IEnumerable<ICapture^>^ const mCaptures;
	};
}
