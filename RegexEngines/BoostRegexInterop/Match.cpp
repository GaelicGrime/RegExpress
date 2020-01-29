#include "pch.h"

#include "Group.h"
#include "Match.h"
#include "Matcher.h"


using namespace System::Collections::Generic;


namespace BoostRegexInterop
{

	Match::Match( Matcher^ parent, const boost::wcmatch& match )
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

			msclr::interop::marshal_context mc{};

			Dictionary<int, String^>^ names = nullptr;

			if( mParent->GroupNames )
			{
				names = gcnew Dictionary<int, String^>( mParent->GroupNames->Count ); // (or use array?)

				for each( auto name0 in mParent->GroupNames )
				{
					const wchar_t* name = mc.marshal_as<const wchar_t*>( name0 );

					int i = match.named_subexpression_index( name, name + wcslen( name ) );
					if( i >= 0 )
					{
						names[i] = name0;
					}
				}
			}

			for( auto i = match.begin( ); i != match.end( ); ++i, ++j )
			{
				const boost::wcsub_match& submatch = *i;

				String^ name = nullptr;
				if( names && !names->TryGetValue( j, name ) ) name = j.ToString( System::Globalization::CultureInfo::InvariantCulture );

				if( !submatch.matched )
				{
					auto group = gcnew Group( this, name );

					mGroups->Add( group );
				}
				else
				{
					int submatch_index = match.position( j );

					auto group = gcnew Group( this, name, submatch_index, submatch );

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
		catch( Exception ^ exc )
		{
			throw exc;
		}
		catch( ... )
		{
			// TODO: also catch 'boost::exception'?
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}


	String^ Match::Value::get( )
	{
		const MatcherData* data = mParent->GetData( );

		return gcnew String( data->mText.c_str( ), mIndex, mLength );
	}
}
