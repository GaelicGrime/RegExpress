#include "pch.h"
#include "CppCapture.h"
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
		try
		{
			mCaptures = gcnew List<ICapture^>;

			const MatcherData* d = parent->Parent->GetData( );

			for( const boost::wcsub_match& c : submatch.captures( ) )
			{
				int index = c.first - d->mText.c_str( );

				CppCapture^ capture = gcnew CppCapture( this, index, c );

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
		catch( ... )
		{
			// TODO: also catch 'boost::exception'?
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}


	String^ CppGroup::Value::get( )
	{
		const MatcherData* data = mParent->Parent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}

}
