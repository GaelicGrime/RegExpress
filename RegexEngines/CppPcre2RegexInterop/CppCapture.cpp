#include "pch.h"
#include "CppCapture.h"
#include "CppGroup.h"
#include "CppMatch.h"
#include "CppMatcher.h"


namespace CppPcre2RegexInterop
{

	CppCapture::CppCapture( CppGroup^ parent, int index, const pcre2_match_data* match )
		:
		mParent( parent ),
		mIndex( index ), // TODO: deals with overflows
		mLength( 0 ) //................. TODO: implement
	{

	}


	String^ CppCapture::Value::get( )
	{
		const MatcherData* data = mParent->Parent->Parent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}

}
