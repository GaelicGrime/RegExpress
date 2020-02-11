#pragma once


using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure::Matches;


namespace Pcre2RegexInterop
{
	ref class Group;


	ref class Capture : ICapture
	{

	public:

		Capture( Group^ parent, int index, const pcre2_match_data* match );


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


	private:

		Group^ const mParent;
		int const mIndex;
		int const mLength;
	};

}