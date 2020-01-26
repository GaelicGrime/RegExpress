#include "pch.h"
#include "CppGroup.h"
#include "CppMatch.h"
#include "CppMatcher.h"


namespace CppPcre2RegexInterop
{

	CppMatch::CppMatch( CppMatcher^ parent, const pcre2_match_data * match )
		:
		mParent( parent ),
		mSuccess( false ), //.................
		mIndex( 0 ), // TODO: deals with overflows
		mLength( 0 ) // TODO: deals with overflows
	{
		try
		{
			mGroups = gcnew List<IGroup^>;

			/*
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
			*/
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


	String^ CppMatch::Value::get( )
	{
		const MatcherData* data = mParent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}
}
