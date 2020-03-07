#include "pch.h"

#include "Group.h"
#include "Match.h"
#include "Matcher.h"


namespace StdRegexInterop
{

	Group::Group( Match^ parent, int groupNumber )
		:
		mParent( parent ),
		mGroupNumber( groupNumber ),
		mSuccess( false ),
		mIndex( 0 ),
		mLength( 0 ),
		mCaptures( gcnew List<ICapture^> )
	{

	}


	Group::Group( Match^ parent, int groupNumber, int index, const std::wcsub_match& submatch )
		:
		mParent( parent ),
		mGroupNumber( groupNumber ),
		mSuccess( submatch.matched ),
		mIndex( index ),
		mLength( CheckedCast::ToInt32( submatch.length( ) ) ),
		mCaptures( gcnew List<ICapture^> )
	{
		// TODO: collect captures, if supported
	}


	String^ Group::Value::get( )
	{
		if( !mSuccess ) return String::Empty;

		const MatcherData* data = mParent->Parent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}

}
