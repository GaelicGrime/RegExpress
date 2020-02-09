#include "pch.h"

#include "Capture.h"
#include "Group.h"
#include "Match.h"
#include "Matcher.h"


namespace Pcre2RegexInterop
{

	Capture::Capture( Group^ parent, int index, const pcre2_match_data* match )
		:
		mParent( parent ),
		mIndex( index ), // TODO: deals with overflows
		mLength( 0 ) //................. TODO: implement
	{

	}


	String^ Capture::Value::get( )
	{
		const MatcherData* data = mParent->Parent->Parent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}

}
