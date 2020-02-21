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
		mLength( static_cast<decltype( mLength )>( submatch.length( ) ) ),
		mCaptures( gcnew List<ICapture^> )
	{
		auto len = submatch.length( );
		if( len < std::numeric_limits<decltype( mLength )>::min( ) || len > std::numeric_limits<decltype( mLength )>::max( ) )
		{
			throw gcnew OverflowException( );
		}

		// TODO: collect captures, if supported
	}


	String^ Group::Value::get( )
	{
		if( !mSuccess ) return String::Empty;

		const MatcherData* data = mParent->Parent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}

}
