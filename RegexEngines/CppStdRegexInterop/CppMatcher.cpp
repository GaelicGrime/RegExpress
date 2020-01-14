#include "pch.h"

#include "CppMatcher.h"
#include "CppMatch.h"


using namespace System::Diagnostics;

using namespace std;
using namespace msclr::interop;


namespace CppStdRegexInterop
{

	CppMatcher::CppMatcher( String^ pattern0, cli::array<String^>^ options )
		: mData( nullptr )
	{
		marshal_context context{};
		wregex::flag_type regex_flags{};
		regex_constants::match_flag_type match_flags = regex_constants::match_flag_type::match_default;

		wstring pattern = context.marshal_as<wstring>( pattern0 );

		for each( String ^ o in options )
		{
#define C(n) \
	if( o == L#n ) regex_flags |= regex_constants::syntax_option_type::##n; \
	else

			C( ECMAScript )
				C( basic )
				C( extended )
				C( awk )
				C( grep )
				C( egrep )
				C( icase )
				C( nosubs )
				C( optimize )
				C( collate )
				;

#undef C

#define C(n) \
	if( o == L#n ) match_flags |= regex_constants::match_flag_type::##n; \
	else

			C( match_not_bol )
				C( match_not_eol )
				C( match_not_bow )
				C( match_not_eow )
				C( match_any )
				C( match_not_null )
				C( match_continuous )
				C( match_prev_avail )
				;

#undef C

		}

		mData = new MatcherData{};
		mData->mMatchFlags = match_flags;

		try
		{
			mData->mRegex.assign( pattern, regex_flags );
		}
		catch( const regex_error & exc )
		{
			//regex_constants::error_type code = exc.code( );
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( what );
		}
		catch( const exception & exc )
		{
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( "Error: " + what );
		}
		catch( ... )
		{
			throw gcnew Exception( "Unknown error." );
		}
	}


	CppMatcher::~CppMatcher( )
	{
		this->!CppMatcher( );
	}


	CppMatcher::!CppMatcher( )
	{
		delete mData;
		mData = nullptr;
	}


	RegexMatches^ CppMatcher::Matches( String^ text0 )
	{
		// TODO: re-implement as lazy enumerator?

		marshal_context context{};

		mData->mText = context.marshal_as<wstring>( text0 );

		auto matches = gcnew List<IMatch^>( );

		auto* native_text = mData->mText.c_str( );

		wcregex_iterator results_begin( native_text, native_text + mData->mText.length( ), mData->mRegex, mData->mMatchFlags );
		wcregex_iterator results_end{};

		for( auto i = results_begin; i != results_end; ++i )
		{
			const wcmatch& match = *i;

			auto m = gcnew CppMatch( this, match );

			matches->Add( m );
		}

		return gcnew RegexMatches( matches->Count, matches );
	}

}