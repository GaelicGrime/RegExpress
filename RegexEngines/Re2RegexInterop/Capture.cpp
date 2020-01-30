#include "pch.h"

#include "Capture.h"
#include "Group.h"
#include "Match.h"
#include "Matcher.h"


namespace Re2RegexInterop
{

	Capture::Capture( Group^ parent, int index, int length )
		:
		mParent( parent ),
		mIndex( index ), // TODO: deals with overflows
		mLength( length ) 
	{

	}


	String^ Capture::Value::get( )
	{
		const MatcherData* data = mParent->Parent->Parent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}

}
