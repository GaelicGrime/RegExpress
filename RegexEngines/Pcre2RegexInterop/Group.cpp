#include "pch.h"

#include "Capture.h"
#include "Group.h"
#include "Match.h"
#include "Matcher.h"


namespace Pcre2RegexInterop
{

	Group::Group( Match^ parent, int groupNumber )
		:
		mParent( parent ),
		mGroupNumber( groupNumber ),
		mSuccess( false ),
		mIndex( 0 ),
		mLength( 0 )
	{
		mCaptures = gcnew List<ICapture^>;

	}


	Group::Group( Match^ parent, int groupNumber, int index, const pcre2_match_data* submatch )
		:
		mParent( parent ),
		mGroupNumber( groupNumber ),
		mSuccess( false ), //..............................
		mIndex( index ), // TODO: deals with overflows
		mLength( 0 ) // .....................
	{
		try
		{
			mCaptures = gcnew List<ICapture^>;

			//...............
			/*

			const MatcherData* d = parent->Parent->GetData( );

			for( const boost::wcsub_match& c : submatch.captures( ) )
			{
				if( !c.matched ) continue;

				int index = c.first - d->mText.c_str( );

				Capture^ capture = gcnew Capture( this, index, c );

				mCaptures->Add( capture );
			}
			*/


			//.............
			//TODO: detect PCRE2 errors
		}
		catch( const std::exception & exc )
		{
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( "Error: " + what );
		}
		catch( ... )
		{
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}


	String^ Group::Value::get( )
	{
		const MatcherData* data = mParent->Parent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}

}
