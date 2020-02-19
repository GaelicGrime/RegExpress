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
		mLength( static_cast<decltype( mLength )>( match.length( ) ) )
	{
		auto len = match.length( );
		if( len < std::numeric_limits<decltype( mLength )>::min( ) || len > std::numeric_limits<decltype( mLength )>::max( ) )
		{
			throw gcnew OverflowException( );
		}
	}


	String^ Capture::Value::get( )
	{
		const MatcherData* data = mParent->Parent->Parent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}

}
