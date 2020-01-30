#pragma once

using namespace System;
using namespace System::Collections::Generic;
using namespace System::Linq;

using namespace RegexEngineInfrastructure::Matches;


namespace BoostRegexInterop
{
	ref class Match;


	ref class Group : public IGroup
	{
	public:

		Group( Match^ parent, String^ name );
		Group( Match^ parent, String^ name, int index, const boost::wcsub_match& submatch );


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
				return mName;
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


		property Match^ Parent
		{
			Match^ get( )
			{
				return mParent;
			}
		}


	private:

		Match^ const mParent;

		bool const mSuccess;
		int const mIndex;
		int const mLength;
		String^ const mName;

		IList<ICapture^>^ const mCaptures;
	};
}
