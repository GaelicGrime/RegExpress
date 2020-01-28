#pragma once

using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure::Matches;


namespace Pcre2RegexInterop
{
	ref class Match;


	ref class Group : public IGroup
	{
	public:

		Group( Match^ parent, int groupNumber );
		Group( Match^ parent, int groupNumber, int index, const pcre2_match_data * submatch );


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
				return mGroupNumber.ToString( ); // TODO: culture?
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
		int const mGroupNumber;

		IList<ICapture^>^ mCaptures;
	};
}
