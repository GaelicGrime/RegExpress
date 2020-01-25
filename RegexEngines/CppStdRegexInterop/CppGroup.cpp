#include "pch.h"
#include "CppGroup.h"
#include "CppMatch.h"
#include "CppMatcher.h"


namespace CppStdRegexInterop
{

	CppGroup::CppGroup( CppMatch^ parent, int groupNumber )
		:
		mParent( parent ),
		mGroupNumber( groupNumber ),
		mSuccess( false ),
		mIndex( 0 ),
		mLength( 0 )
	{
		mCaptures = gcnew List<ICapture^>;

	}


	CppGroup::CppGroup( CppMatch^ parent, int groupNumber, int index, const std::wcsub_match& submatch )
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
