#include "pch.h"

#include "Matcher.h"
#include "Match.h"


using namespace System::Diagnostics;

using namespace boost;
using namespace msclr::interop;


namespace BoostRegexInterop
{

	Matcher::Matcher( String^ pattern0, cli::array<String^>^ options )
		: mData( nullptr )
	{
		try
		{
			marshal_context context{};
			wregex::flag_type regex_flags{};
			regex_constants::match_flag_type match_flags = regex_constants::match_flag_type::match_default;

			std::wstring pattern = context.marshal_as<std::wstring>( pattern0 );

			for each( String ^ o in options )
			{
#define C(n) \
	if( o == L#n ) regex_flags |= regex_constants::##n; \
	else

				C( normal )
					C( ECMAScript )
					C( JavaScript )
					C( JScript )
					C( perl )
					C( basic )
					C( sed )
					C( extended )
					C( awk )
					C( grep )
					C( egrep )
					C( emacs )
					C( literal )

					C( icase )
					C( nosubs )
					C( optimize )
					C( collate )
					//?C( newline_alt ) // wrong documentation?
					C( no_except )
					C( save_subexpression_location )
					C( no_mod_m )
					C( no_mod_s )
					C( mod_s )
					C( mod_x )
					C( no_empty_expressions )
					C( no_escape_in_lists )
					//?C( no_bk_refs ) // wrong documentation?
					C( no_char_classes )
					C( no_intervals )
					C( bk_plus_qm )
					C( bk_vbar )

					;

#undef C

#define C(n) \
	if( o == L#n ) match_flags |= regex_constants::match_flag_type::##n; \
	else

				C( match_default )
					C( match_not_bob )
					C( match_not_eob )
					C( match_not_bol )
					C( match_not_eol )
					C( match_not_bow )
					C( match_not_eow )
					C( match_any )
					C( match_not_null )
					C( match_continuous )
					C( match_partial )
					C( match_single_line )
					C( match_prev_avail )
					C( match_not_dot_newline )
					C( match_not_dot_null )
					C( match_posix )
					C( match_perl )
					C( match_nosubs )
					C( match_extra )
					;
#undef C
			}

			mData = new MatcherData{};
			mData->mMatchFlags = match_flags;

			mData->mRegex.assign( std::move( pattern ), regex_flags );
		}
		catch( const regex_error & exc )
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
		catch( ... )
		{
			// TODO: also catch 'boost::exception'?
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}


	Matcher::~Matcher( )
	{
		this->!Matcher( );
	}


	Matcher::!Matcher( )
	{
		delete mData;
		mData = nullptr;
	}


	String^ Matcher::GetBoostVersion( )
	{
		/*
		For Boost Documentation:
			BOOST_VERSION
			<boost/version.hpp>
			Describes the boost version number in XYYYZZ format such that:
			(BOOST_VERSION % 100) is the sub-minor version,
			((BOOST_VERSION / 100) % 1000) is the minor version,
			and (BOOST_VERSION / 100000) is the major version.
		*/

		return String::Format( "{0}.{1}.{2}", BOOST_VERSION / 100000, ( BOOST_VERSION / 100 ) % 1000, ( BOOST_VERSION % 100 ).ToString( ) );
		// 'ToString' -- to remove warning C4965 -- "Implicit box of integer 0; use nullptr or explicit cast"

	}

	RegexMatches^ Matcher::Matches( String^ text0 )
	{
		// TODO: re-implement as lazy enumerator?

		try
		{
			marshal_context context{};

			mData->mText = context.marshal_as<std::wstring>( text0 );

			auto matches = gcnew List<IMatch^>( );

			auto* native_text = mData->mText.c_str( );

			wcregex_iterator results_begin( native_text, native_text + mData->mText.length( ), mData->mRegex, mData->mMatchFlags );
			wcregex_iterator results_end{};

			for( auto i = results_begin; i != results_end; ++i )
			{
				const wcmatch& match = *i;

				auto m = gcnew Match( this, match );

				matches->Add( m );
			}

			return gcnew RegexMatches( matches->Count, matches );
		}
		catch( const regex_error & exc )
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
		catch( ... )
		{
			// TODO: also catch 'boost::exception'?
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}

}
