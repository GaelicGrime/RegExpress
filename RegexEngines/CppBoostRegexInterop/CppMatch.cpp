#include "pch.h"
#include "CppGroup.h"
#include "CppMatch.h"
#include "CppMatcher.h"


namespace CppBoostRegexInterop
{

	CppMatch::CppMatch( CppMatcher^ parent, const boost::wcmatch& match )
		:
		mParent( parent ),
		mSuccess( !match.empty( ) ),
		mIndex( match.position( ) ), // TODO: deals with overflows
		mLength( match.length( ) ) // TODO: deals with overflows
	{
		try
		{
			mGroups = gcnew List<IGroup^>;
			int j = 0;

			for( auto i = match.begin( ); i != match.end( ); ++i, ++j )
			{
				const boost::wcsub_match& submatch = *i;

				if( !submatch.matched )
				{
					auto group = gcnew CppGroup( this, j );

					mGroups->Add( group );
				}
				else
				{
					int submatch_index = match.position( j );

					auto group = gcnew CppGroup( this, j, submatch_index, submatch );

					mGroups->Add( group );
				}
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


	String^ CppMatch::Value::get( )
	{
		const MatcherData* data = mParent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}
}
