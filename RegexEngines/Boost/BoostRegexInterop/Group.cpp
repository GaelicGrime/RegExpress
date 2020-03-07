#include "pch.h"

#include "Capture.h"
#include "Group.h"
#include "Match.h"
#include "Matcher.h"


namespace BoostRegexInterop
{

	Group::Group( Match^ parent, String^ name )
		:
		mParent( parent ),
		mName( name ),
		mSuccess( false ),
		mIndex( 0 ),
		mLength( 0 ),
		mCaptures( gcnew List<ICapture^> )
	{

	}


	Group::Group( Match^ parent, String^ name, int index, const boost::wcsub_match& submatch )
		:
		mParent( parent ),
		mName( name ),
		mSuccess( submatch.matched ),
		mIndex( index ),
		mLength( CheckedCast::ToInt32( submatch.length( ) ) ),
		mCaptures( gcnew List<ICapture^> )
	{
		try
		{
			const MatcherData* d = parent->Parent->GetData( );

			for( const boost::wcsub_match& c : submatch.captures( ) )
			{
				if( !c.matched ) continue;

				auto index = c.first - d->mText.c_str( );

				Capture^ capture = gcnew Capture( this, CheckedCast::ToInt32( index ), c );

				mCaptures->Add( capture );
			}
		}
		catch( const boost::regex_error & exc )
		{
			//regex_constants::error_type code = exc.code( );
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( what );
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
			// TODO: also catch 'boost::exception'?
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
