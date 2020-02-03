#pragma once

using namespace System;
using namespace System::Collections::Generic;

using namespace RegexEngineInfrastructure::Matches;


namespace Re2RegexInterop
{
	ref class Group;
	ref class Matcher;

	ref class Match : IMatch
	{

	public:

		Match( Matcher^ parent, int index, int length );

		void AddGroup( Group^ g );


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
				System::Diagnostics::Debug::Assert( false );

				return nullptr; // not expected to be called; only group's name is needed
			}
		}

		virtual property IEnumerable<ICapture^>^ Captures
		{
			IEnumerable<ICapture^>^ get( )
			{
				System::Diagnostics::Debug::Assert( false );

				return nullptr; // not expected to be called; only group's captures are needed
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


		property Matcher^ Parent
		{
			Matcher^ get( )
			{
				return mParent;
			}
		}

	private:

		Matcher^ const mParent;
		bool const mSuccess;
		int const mIndex;
		int const mLength;

		List<IGroup^>^ const mGroups;
	};

}
