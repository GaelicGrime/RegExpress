#pragma once

using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure::Matches;


namespace OnigurumaRegexInterop
{
	ref class Capture;
	ref class Match;


	ref class Group : IGroup
	{

	public:

		Group( Match^ parent, String^ name, bool success, int index, int length );

		void AddCapture( Capture^ capture );

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

	internal:

		void SetName( String^ name )
		{
			mName = name;
		}

	private:

		Match^ const mParent;

		bool const mSuccess;
		int const mIndex;
		int const mLength;
		String^ mName;

		IList<ICapture^>^ const mCaptures;
	};
}
