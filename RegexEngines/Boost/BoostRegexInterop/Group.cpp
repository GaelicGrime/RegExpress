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
		mLength( static_cast<decltype( mLength )>( submatch.length( ) ) ),
		mCaptures( gcnew List<ICapture^> )
	{
		auto len = submatch.length( );
		if( len < std::numeric_limits<decltype( mLength )>::min( ) || len > std::numeric_limits<decltype( mLength )>::max( ) )
		{
			throw gcnew OverflowException( );
		}

		try
		{
			const MatcherData* d = parent->Parent->GetData( );

			for( const boost::wcsub_match& c : submatch.captures( ) )
			{
				if( !c.matched ) continue;

				auto index = c.first - d->mText.c_str( );
				if( index < 0 || index > std::numeric_limits<int>::max( ) )
				{
					throw gcnew OverflowException( );
				}

				Capture^ capture = gcnew Capture( this, static_cast<int>( index ), c );

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
			throw exc;
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
