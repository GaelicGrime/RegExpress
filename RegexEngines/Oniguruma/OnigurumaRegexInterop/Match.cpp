#include "pch.h"

#include "Group.h"
#include "Match.h"
#include "Matcher.h"


using namespace System::Diagnostics;


namespace OnigurumaRegexInterop
{

	Match::Match( Matcher^ parent, int index, int length )
		:
		mParent( parent ),
		mSuccess( true ),
		mIndex( index ), // TODO: deals with overflows
		mLength( length ), // TODO: deals with overflows
		mGroups( gcnew List<IGroup^> )
	{
		try
		{

			//

		}
		catch( const std::exception & exc )
		{
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( "Error: " + what );
		}
		catch( Exception ^ exc )
		{
			throw exc;
		}
		catch( ... )
		{
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}


	void Match::AddGroup( Group^ g )
	{
		Debug::Assert( !mGroups->Contains( g ) );

		mGroups->Add( g );
	}



	String^ Match::Value::get( )
	{
		//const MatcherData* data = mParent->GetData( );

		return mParent->OriginalText->Substring( mIndex, mLength );
	}

}
