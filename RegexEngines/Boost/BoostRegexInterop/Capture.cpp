#include "pch.h"

#include "Capture.h"
#include "Group.h"
#include "Match.h"
#include "Matcher.h"


namespace BoostRegexInterop
{

	Capture::Capture( Group^ parent, int index, const boost::wcsub_match& match )
		:
		mParent( parent ),
		mIndex( index ),
		mLength( CheckedCast::ToInt32( match.length( ) ) )
	{
	}


	String^ Capture::Value::get( )
	{
		const MatcherData* data = mParent->Parent->Parent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}

}
