#include "pch.h"

#include "Group.h"
#include "Match.h"
#include "Matcher.h"


using namespace System::Diagnostics;


namespace Re2RegexInterop
{

	Match::Match( Matcher^ parent )
		:
		mParent( parent ),
		mSuccess( false ),
		mIndex( 0 ), // TODO: deals with overflows
		mLength( 0 ), // TODO: deals with overflows
		mGroups( gcnew List<IGroup^> )
	{
		try
		{

			//.............

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
