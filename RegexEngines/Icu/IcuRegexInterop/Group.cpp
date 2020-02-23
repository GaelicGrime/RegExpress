#include "pch.h"

#include "Capture.h"
#include "Group.h"
#include "Match.h"
#include "Matcher.h"


using namespace System::Diagnostics;


namespace IcuRegexInterop
{

	Group::Group( Match^ parent, String^ name, bool success, int index, int length )
		:
		mParent( parent ),
		mName( name ),
		mSuccess( success ),
		mIndex( index ), // TODO: deals with overflows
		mLength( length ),
		mCaptures( gcnew List<ICapture^>( ) )
	{
	}


	void Group::AddCapture( Capture^ capture )
	{
		Debug::Assert( !mCaptures->Contains( capture ) );

		mCaptures->Add( capture );
	}


	String^ Group::Value::get( )
	{
		if( !mSuccess ) return String::Empty;

		return mParent->Parent->OriginalText->Substring( mIndex, mLength );
	}

}
