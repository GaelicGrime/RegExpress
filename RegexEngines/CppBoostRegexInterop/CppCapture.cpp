#include "pch.h"
#include "CppCapture.h"
#include "CppGroup.h"
#include "CppMatch.h"
#include "CppMatcher.h"


namespace CppBoostRegexInterop
{

	CppCapture::CppCapture( CppGroup^ parent, int index, const boost::wcsub_match& match )
		:
		mParent( parent ),
		mIndex( index ), // TODO: deals with overflows
		mLength( match.length( ) ) // TODO: deals with overflows
	{

	}


	String^ CppCapture::Value::get( )
	{
		const MatcherData* data = mParent->Parent->Parent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}

}
