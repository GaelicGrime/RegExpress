#include "pch.h"

#include "Group.h"
#include "Match.h"
#include "Matcher.h"


using namespace System::Diagnostics;


namespace Pcre2RegexInterop
{

	Match::Match( Matcher^ parent, pcre2_code* re, PCRE2_SIZE* ovector, int rc )
		:
		mParent( parent ),
		mSuccess( ovector[0] >= 0 ),
		mIndex( static_cast<int>( ovector[0] ) ), // TODO: deals with overflows
		mLength( static_cast<int>( ovector[1] - ovector[0] ) ), // TODO: deals with overflows
		mGroups( gcnew List<IGroup^> )
	{
		Debug::Assert( rc > 0 );

		try
		{
			if( ovector[0] > ovector[1] )
			{
				// TODO: show more details; see 'pcre2demo.c'
				throw gcnew Exception( String::Format( "PCRE2 Error: {0}",
					"\\K was used in an assertion to set the match start after its end." ) );
			}

			// add all groups; the names will be put later
			// group [0] is the whole match

			for( int i = 0; i < rc; ++i )
			{
				Group^ group = gcnew Group( this,
					i.ToString( System::Globalization::CultureInfo::InvariantCulture ),
					static_cast<int>( ovector[2 * i] ),
					static_cast<int>( ovector[2 * i + 1] - ovector[2 * i] ) );
				mGroups->Add( group );
			}

			// add failed groups not included in 'rc'
			{
				uint32_t capturecount;

				if( pcre2_pattern_info(
					re,
					PCRE2_INFO_CAPTURECOUNT,
					&capturecount ) == 0 )
				{
					for( int i = rc; i <= (int)capturecount; ++i )
					{
						Group^ group = gcnew Group( this,
							i.ToString( System::Globalization::CultureInfo::InvariantCulture ),
							-1, 0 );
						mGroups->Add( group );
					}
				}
			}

			uint32_t namecount;

			(void)pcre2_pattern_info(
				re,                   /* the compiled pattern */
				PCRE2_INFO_NAMECOUNT, /* get the number of named substrings */
				&namecount );         /* where to put the answer */

			if( namecount > 0 )
			{
				PCRE2_SPTR name_table;
				uint32_t name_entry_size;
				PCRE2_SPTR tabptr;

				(void)pcre2_pattern_info(
					re,                       /* the compiled pattern */
					PCRE2_INFO_NAMETABLE,     /* address of the table */
					&name_table );            /* where to put the answer */

				(void)pcre2_pattern_info(
					re,                       /* the compiled pattern */
					PCRE2_INFO_NAMEENTRYSIZE, /* size of each entry in the table */
					&name_entry_size );       /* where to put the answer */

				tabptr = name_table;
				for( int i = 0; i < (int)namecount; i++ )
				{
					int n = *( (__int16*)tabptr );

					String^ name = gcnew String( reinterpret_cast<const wchar_t*>( ( (__int16*)tabptr ) + 1 ), 0, name_entry_size - 2 );
					name = name->TrimEnd( '\0' );

					( (Group^)mGroups[n] )->SetName( name );

					tabptr += name_entry_size;
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
			throw exc;
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
