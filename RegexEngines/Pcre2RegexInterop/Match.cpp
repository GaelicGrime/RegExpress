#include "pch.h"

#include "Group.h"
#include "Match.h"
#include "Matcher.h"


namespace Pcre2RegexInterop
{

	Match::Match( Matcher^ parent, pcre2_match_data* matchData )
		:
		mParent( parent ),
		mSuccess( true ) //.................
	{
		try
		{
			PCRE2_SIZE* ovector = ovector = pcre2_get_ovector_pointer( matchData );

			if( ovector[0] > ovector[1] )
			{
				// TODO: show more details; see 'pcre2demo.c'
				throw gcnew Exception( String::Format( "PCRE2 Error: {0}",
					"\\K was used in an assertion to set the match start after its end." ) );
			}

			mIndex = ovector[0]; // TODO: deals with overflows
			mLength = ovector[1] - ovector[0]; // TODO: deals with overflows

			mGroups = gcnew List<IGroup^>;

			// group [0] is the whole match

			Group^ group0 = gcnew Group( this, "0", mIndex, mLength );
			mGroups->Add( group0 );





			//.......................

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
