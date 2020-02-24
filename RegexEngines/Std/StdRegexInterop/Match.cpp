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
		mIndex( static_cast<decltype( mIndex )>( match.position( ) ) ),
		mLength( static_cast<decltype( mLength )>( match.length( ) ) )
	{
		try
		{
			auto pos = match.position( );
			if( pos < std::numeric_limits<decltype( mIndex )>::min( ) || pos > std::numeric_limits<decltype( mIndex )>::max( ) )
			{
				throw gcnew OverflowException( );
			}

			auto len = match.length( );
			if( len < std::numeric_limits<decltype( mLength )>::min( ) || len > std::numeric_limits<decltype( mLength )>::max( ) )
			{
				throw gcnew OverflowException( );
			}


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
					if( pos < std::numeric_limits<int>::min( ) || pos > std::numeric_limits<int>::max( ) )
					{
						throw gcnew OverflowException( );
					}

					int submatch_index = static_cast<int>( pos );

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
		catch( Exception ^ )
		{
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
