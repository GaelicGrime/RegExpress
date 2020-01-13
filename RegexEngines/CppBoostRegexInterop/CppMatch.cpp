#include "pch.h"
#include "CppMatch.h"
#include "CppMatcher.h"



namespace CppStdRegexInterop
{

	CppMatch::CppMatch( CppMatcher^ parent, const boost::wcmatch& match )
		:
		mParent( parent ),
		mSuccess( !match.empty( ) ),
		mIndex( match.position( ) ), // TODO: deals with overflows
		mLength( match.length( ) ) // TODO: deals with overflows
	{
 		mGroups = gcnew List<IGroup^>;
		int j = 0;

		for( auto i = match.begin( ); i != match.end( ); ++i, ++j )
		{
			int submatch_index = match.position( j );
			boost::wcsub_match submatch = *i;

			auto group = gcnew CppGroup( this, j, submatch_index, submatch );

			mGroups->Add( group );
		}
	}


	String^ CppMatch::Value::get( )
	{
		const MatcherData* data = mParent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}
}
