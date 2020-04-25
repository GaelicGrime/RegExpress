#include "pch.h"

#include "Matcher.h"


using namespace System::Diagnostics;
using namespace System::Text::RegularExpressions;
using namespace System::Collections::Specialized;


using namespace boost;
using namespace msclr::interop;


namespace BoostRegexInterop
{

	static Matcher::Matcher( )
	{
		mRegexGroupNames = gcnew Regex(
			//R"REGEX( \(\? ((?'a'')|<) (?'n'\p{L}\w*?) (?(a)'|>) )REGEX",
			R"REGEX( \(\? ((?'a'')|<) (?'n'.*?) (?(a)'|>) )REGEX",
			RegexOptions::Compiled | RegexOptions::ExplicitCapture | RegexOptions::IgnorePatternWhitespace
		);

		BuildOptions( );
	}


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

			//?auto nsubs = mData->mRegex.get_named_subs( );

			// try identifying group names

			mGroupNames = nullptr;

			auto matches = mRegexGroupNames->Matches( pattern0 );
			if( matches->Count > 0 )
			{
				mGroupNames = gcnew StringCollection;

				for( int i = 0; i < matches->Count; ++i )
				{
					auto n = matches[i]->Groups["n"];
					if( n->Success )
					{
						mGroupNames->Add( n->Value );
					}
				}
			}
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
		catch( Exception ^ exc )
		{
			UNREFERENCED_PARAMETER( exc );
			throw;
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
		From Boost documentation:
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

	RegexMatches^ Matcher::Matches( String^ text0, ICancellable^ cnc )
	{
		try
		{
			marshal_context mc{};

			mData->mText = mc.marshal_as<std::wstring>( text0 );

			auto matches = gcnew List<IMatch^>( );

			auto* native_text = mData->mText.c_str( );

			wcregex_iterator results_begin( native_text, native_text + mData->mText.length( ), mData->mRegex, mData->mMatchFlags );
			wcregex_iterator results_end{};

			for( auto i = results_begin; i != results_end; ++i )
			{
				const wcmatch& match = *i;

				Dictionary<int, String^>^ names = nullptr;

				if( GroupNames )
				{
					names = gcnew Dictionary<int, String^>( GroupNames->Count ); // (or use array?)

					for each( auto name0 in GroupNames )
					{
						const wchar_t* name = mc.marshal_as<const wchar_t*>( name0 );

						int i = match.named_subexpression_index( name, name + wcslen( name ) );
						if( i >= 0 )
						{
							names[i] = name0;
						}
					}
				}

				auto m = CreateMatch( match, names );
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
		catch( Exception ^ exc )
		{
			UNREFERENCED_PARAMETER( exc );
			throw;
		}
		catch( ... )
		{
			// TODO: also catch 'boost::exception'?
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}


	String^ Matcher::GetText( int index, int length )
	{
		return gcnew String( mData->mText.c_str( ), index, length );
	}


	IMatch^ Matcher::CreateMatch( const wcmatch& match, Dictionary<int, String^>^ names )
	{
		auto m = SimpleMatch::Create( CheckedCast::ToInt32( match.position( ) ), CheckedCast::ToInt32( match.length( ) ), this );

		int j = 0;

		for( auto i = match.begin( ); i != match.end( ); ++i, ++j )
		{
			const boost::wcsub_match& submatch = *i;

			String^ name = nullptr;
			if( !names || !names->TryGetValue( j, name ) ) name = j.ToString( System::Globalization::CultureInfo::InvariantCulture );

			if( !submatch.matched )
			{
				m->AddGroup( 0, 0, false, name );
			}
			else
			{
				auto submatch_index = match.position( j );

				auto group = m->AddGroup( CheckedCast::ToInt32( submatch_index ), CheckedCast::ToInt32( submatch.length( ) ), true, name );

				for( const boost::wcsub_match& c : submatch.captures( ) )
				{
					if( !c.matched ) continue;

					auto index = c.first - mData->mText.c_str( );

					// WORKAROUND for an apparent problem of Boost Regex: the collection includes captures from other groups
					if( index < m->Index ) continue;

					group->AddCapture( CheckedCast::ToInt32( index ), CheckedCast::ToInt32( c.length( ) ) );
				}
			}
		}

		return m;
	}


	void Matcher::BuildOptions( )
	{
#define C(f, n) \
	list->Add(gcnew OptionInfo( regex_constants::##f, gcnew String(#f), gcnew String(n)));

		List<OptionInfo^>^ list = gcnew List<OptionInfo^>( );

		C( icase, "without regard to case" );
		C( nosubs, "no sub-expression matches are to be stored" );
		C( optimize, "currently has no effect for Boost.Regex" );
		C( collate, "character ranges of the form [a-b] should be locale sensitive" );
		//?C( newline_alt, "the \\n character has the same effect as the alternation operator |" );
		C( no_except, "prevents from throwing an exception when an invalid expression is encountered" );
		C( no_mod_m, "disable m modifier" );
		C( no_mod_s, "force s modifier off" );
		C( mod_s, "match \".\" against a newline character" );
		C( mod_x, "causes unescaped whitespace in the expression to be ignored" );
		C( no_empty_expressions, "empty expressions/alternatives are prohibited" );
		//save_subexpression_location, When set then the locations of individual sub-expressions within the original regular expression string can be accessed via the subexpression() member function of basic_regex. 

		// TODO: define extra-options too

		mCompileOptions = list;


		list = gcnew List<OptionInfo^>( );

		C( match_not_bob, "\"\\A\" and \"\\`\" should not match against the sub-sequence [first,first)" );
		C( match_not_eob, "\"\\'\", \"\\z\" and \"\\Z\" should not match against the sub-sequence [last,last)" );
		C( match_not_bol, "\"^\" should not be matched against the sub-sequence [first,first)" );
		C( match_not_eol, "\"$\" should not be matched against the sub-sequence [last,last)" );
		C( match_not_bow, "\"\\<\" and \"\\b\" should not be matched against the sub-sequence [first,first)" );
		C( match_not_eow, "\"\\>\" and \"\\b\" should not be matched against the sub-sequence [last,last)" );
		C( match_any, "any match is an acceptable result" );
		C( match_not_null, "the expression can not be matched against an empty sequence" );
		C( match_continuous, "the expression must match a sub-sequence that begins at first" );
		C( match_partial, "find partial matches" );
		C( match_extra, "retain all available capture information" );
		C( match_single_line, "^ only matches at the start of the text, $ only matches at the end of the text" );
		C( match_prev_avail, "valid expression assumed before the start of text" );
		C( match_not_dot_newline, "\".\" does not match a newline character" );
		C( match_not_dot_null, "\".\" does not match a character null '\\0'" );
		C( match_posix, "expression should be matched according to the POSIX leftmost-longest rule" );
		C( match_perl, "the expression should be matched according to the Perl matching rules" );
		C( match_nosubs, "don't trap marked subs" );

		mMatchOptions = list;

#undef C
	}
}
