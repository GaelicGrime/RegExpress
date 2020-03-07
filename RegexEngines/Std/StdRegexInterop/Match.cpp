#include "pch.h"

#include "Group.h"
#include "Match.h"
#include "Matcher.h"



namespace StdRegexInterop
{

	Match::Match( Matcher^ parent, const std::wcmatch& match )
		:
		mParent( parent ),
		mSuccess( !match.empty( ) ),
		mIndex( CheckedCast::ToInt32( match.position( ) ) ),
		mLength( CheckedCast::ToInt32( match.length( ) ) )
	{
		try
		{
			mGroups = gcnew List<IGroup^>;
			int j = 0;

			for( auto i = match.cbegin( ); i != match.cend( ); ++i, ++j )
			{
				const std::wcsub_match& submatch = *i;

				if( !submatch.matched )
				{
					auto group = gcnew Group( this, j );

					mGroups->Add( group );
				}
				else
				{
					auto pos = match.position( j );
					int submatch_index = CheckedCast::ToInt32( pos );

					auto group = gcnew Group( this, j, submatch_index, submatch );

					mGroups->Add( group );
				}
			}
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


	String^ Match::Value::get( )
	{
		const MatcherData* data = mParent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}
}
