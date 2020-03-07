#include "pch.h"

#include "Capture.h"
#include "Group.h"
#include "Match.h"
#include "Matcher.h"


namespace Pcre2RegexInterop
{

	Group::Group( Match^ parent, String^ name, int index, int length )
		:
		mParent( parent ),
		mName( name ),
		mSuccess( index >= 0 ),
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

		const MatcherData* data = mParent->Parent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}

}
