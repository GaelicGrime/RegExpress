#include "pch.h"

#include "Group.h"
#include "Match.h"
#include "Matcher.h"


namespace Pcre2RegexInterop
{

	Match::Match( Matcher^ parent, const pcre2_match_data * match )
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
					auto group = gcnew Group( this, j );

					mGroups->Add( group );
				}
				else
				{
					int submatch_index = match.position( j );

					auto group = gcnew Group( this, j, submatch_index, submatch );

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


	String^ Match::Value::get( )
	{
		const MatcherData* data = mParent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}
}
