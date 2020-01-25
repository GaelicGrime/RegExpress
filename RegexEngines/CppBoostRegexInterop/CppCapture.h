#pragma once


using namespace System;
using namespace System::Collections::Generic;
using namespace System::Linq;

using namespace RegexEngineInfrastructure::Matches;


namespace CppBoostRegexInterop
{
	ref class CppGroup;


	ref class CppCapture : ICapture
	{

	public:

		CppCapture( CppGroup^ parent, int index, const boost::wcsub_match& match );


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

		CppGroup^ const mParent;
		int const mIndex;
		int const mLength;
	};

}
