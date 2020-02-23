#include "pch.h"

#include "Capture.h"
#include "Group.h"
#include "Match.h"
#include "Matcher.h"


namespace IcuRegexInterop
{

	Capture::Capture( Group^ parent, int index, int length )
		:
		mParent( parent ),
		mIndex( index ), // TODO: deals with overflows
		mLength( length ) // TODO: deals with overflows
	{
	}


	String^ Capture::Value::get( )
	{
		//const MatcherData* data = mParent->GetData( );

		return mParent->Parent->Parent->OriginalText->Substring( mIndex, mLength );
	}
}
