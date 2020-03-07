#include "pch.h"

#include "Group.h"
#include "Match.h"
#include "Matcher.h"


namespace Re2RegexInterop
{

	Group::Group( Match^ parent, String^ name, bool success, int index, int length )
		:
		mParent( parent ),
		mName( name ),
		mSuccess( success ),
		mIndex( index ),
		mLength( length ),
		mCaptures( gcnew List<ICapture^>( ) )
	{
		try
		{
			// TODO: implement captures, if supported
		}
		catch( const std::exception & exc )
		{
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( "Error: " + what );
		}
		catch( Exception ^ exc )
		{
			UNREFERENCED_PARAMETER( exc );
			throw;
		}
		catch( ... )
		{
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}


	String^ Group::Value::get( )
	{
		if( !mSuccess ) return String::Empty;

		return mParent->Parent->OriginalText->Substring( mIndex, mLength );
	}
}
