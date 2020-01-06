#pragma once

#include <regex>
#include "CppGroup.h"

using namespace System;
using namespace System::Collections::Generic;
using namespace System::Linq;

using namespace RegexEngineInfrastructure::Matches;


namespace CppRegexEngine
{

	ref class CppMatch : IMatch
	{

	public:

		CppMatch( const std::wsmatch& match )
		{
			mSuccess = !match.empty( );
			mIndex = match.position( );
			mValue = gcnew String( match.str( ).c_str( ) );

			mGroups = gcnew List<IGroup^>;
			int j = 0;

			for( auto i = match.cbegin( ); i != match.cend( ); ++i, ++j )
			{
				int submatch_index = match.position( j );
				std::wssub_match submatch = *i;

				auto group = gcnew CppGroup( submatch_index, submatch );

				mGroups->Add( group );
			}
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

#pragma region IMatch

		virtual property IEnumerable<RegexEngineInfrastructure::Matches::IGroup^>^ Groups
		{
			IEnumerable<IGroup^>^ get( )
			{
				return mGroups;
			}
		}

#pragma endregion IMatch

	private:

		bool mSuccess;
		int mIndex;
		String^ mValue;

		List<IGroup^>^ mGroups;
	};

}
