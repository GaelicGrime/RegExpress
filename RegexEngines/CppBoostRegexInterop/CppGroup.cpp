#include "pch.h"
#include "CppGroup.h"
#include "CppMatch.h"
#include "CppMatcher.h"


namespace CppBoostRegexInterop
{

	CppGroup::CppGroup( CppMatch^ parent, int groupNumber, int index, const boost::wcsub_match& submatch )
		:
		mParent( parent ),
		mGroupNumber( groupNumber ),
		mSuccess( submatch.matched ),
		mIndex( index ), // TODO: deals with overflows
		mLength( submatch.length( ) ) // TODO: deals with overflows
	{
		mCaptures = gcnew List<ICapture^>;

		// TODO: collect captures
	}


	String^ CppGroup::Value::get( )
	{
		const MatcherData* data = mParent->Parent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}

}