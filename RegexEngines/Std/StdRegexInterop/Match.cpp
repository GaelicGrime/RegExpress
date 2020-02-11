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
		mIndex( match.position( ) ), // TODO: deals with overflows
		mLength( match.length( ) ) // TODO: deals with overflows
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
				int submatch_index = match.position( j );

				auto group = gcnew Group( this, j, submatch_index, submatch );

				mGroups->Add( group );
			}
		}
	}


	String^ Match::Value::get( )
	{
		const MatcherData* data = mParent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}
}