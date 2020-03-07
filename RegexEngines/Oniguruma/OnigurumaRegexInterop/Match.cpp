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
		mIndex( index ),
		mLength( length ),
		mGroups( gcnew List<IGroup^> )
	{
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
