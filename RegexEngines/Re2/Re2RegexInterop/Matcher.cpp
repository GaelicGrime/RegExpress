#include "pch.h"

#include "Matcher.h"


using namespace System::Diagnostics;

using namespace msclr::interop;


namespace Re2RegexInterop
{

	static std::map<const wchar_t*, void( RE2::Options::* )( bool ) > mOptionSetters;


	static Matcher::Matcher( )
	{
		mEmptyEnumeration = gcnew List<IMatch^>( 0 );

		BuildOptions( );
	}


	static std::vector<char> ToUtf8( String^ s )
	{
		const char* old_locale = setlocale( LC_ALL, NULL );
		const char* new_locale = setlocale( LC_CTYPE, ".utf8" );

		if( new_locale == nullptr )
		{
			throw gcnew Exception( "Failed to set locale." );
		}

		std::vector<char> utf8( s->Length );

		pin_ptr<const wchar_t> pinned = PtrToStringChars( s );
		const wchar_t* start = pinned;

		auto mb_cur_max = MB_CUR_MAX;
		mbstate_t mbstate = { 0 };

		size_t bytes_written = 0;

		for( const wchar_t* p = start; *p; ++p ) // (we assume that the array is zero-terminated)
		{
			size_t size_converted;
			errno_t error;

			utf8.resize( bytes_written + mb_cur_max );

			error = wcrtomb_s( &size_converted, utf8.data( ) + bytes_written, mb_cur_max, *p, &mbstate );

			if( error )
			{
				setlocale( LC_ALL, old_locale ); // restore

				String^ err = gcnew String( strerror( error ) );

				throw gcnew Exception( String::Format( "Failed to convert to UTF-8: '{0}'. Source index: {1}.", err, p - start ) );
			}

			bytes_written += size_converted;
		}

		utf8.resize( bytes_written + 1, 0 );
		utf8.back( ) = 0; // (zero-terminated)

		setlocale( LC_ALL, old_locale ); // restore

		return utf8;
	}


	void ToUtf8( std::vector<char>* dest, std::vector<int>* indices, String^ s )
	{
		const char* old_locale = setlocale( LC_ALL, NULL );
		const char* new_locale = setlocale( LC_CTYPE, ".utf8" );

		if( new_locale == nullptr )
		{
			throw gcnew Exception( "Failed to set locale." );
		}

		dest->reserve( s->Length + 1 );
		indices->reserve( s->Length + 1 );

		pin_ptr<const wchar_t> pinned = PtrToStringChars( s );
		const wchar_t* start = pinned;

		auto mb_cur_max = MB_CUR_MAX;
		mbstate_t mbstate = { 0 };

		size_t bytes_written = 0;

		for( const wchar_t* p = start; *p; ++p ) // (we assume that the pinned array is zero-terminated)
		{
			indices->resize( bytes_written + 1, -1 ); // ('-1' will denote unset elements)
			( *indices )[bytes_written] = CheckedCast::ToInt32( p - start );

			dest->resize( bytes_written + mb_cur_max );

			size_t size_converted;
			errno_t error;

			error = wcrtomb_s( &size_converted, dest->data( ) + bytes_written, mb_cur_max, *p, &mbstate );

			if( error )
			{
				setlocale( LC_ALL, old_locale ); // restore

				String^ err = gcnew String( strerror( error ) );

				throw gcnew Exception( String::Format( "Failed to convert to UTF-8: '{0}'. Source index: {1}.", err, p - start ) );
			}

			bytes_written += size_converted;
		}

		dest->resize( bytes_written + 1, 0 );
		dest->back( ) = 0; // (zero-terminated)
		indices->resize( bytes_written, -1 );
		indices->push_back( s->Length );

		setlocale( LC_ALL, old_locale ); // restore
	}


	Matcher::Matcher( String^ pattern0, cli::array<String^>^ options )
		: mData( nullptr )
	{
		try
		{
			// TODO: optimise

			RE2::Options re2_options{};

			for( auto i = mOptionSetters.cbegin( ); i != mOptionSetters.cend( ); ++i )
			{
				String^ o = gcnew String( i->first );
				auto f = i->second;
				( re2_options.*f )( Array::IndexOf( options, o ) >= 0 );
			}

			std::vector<char> utf8 = ToUtf8( pattern0 );

			Debug::Assert( utf8.size( ) > 0 );

			re2::StringPiece pattern( utf8.data( ), utf8.size( ) - 1 ); // (zero-terminator excluded)

			std::unique_ptr<RE2> re( new RE2( pattern, re2_options ) );

			if( !re->ok( ) )
			{
				throw gcnew Exception( String::Format( "RE2 Error {0}: {1}", (int)re->error_code( ), gcnew String( re->error( ).c_str( ) ) ) );
			}

			mData = new MatcherData{};

			mData->mRe = std::move( re );

			mData->mAnchor = RE2::Anchor::UNANCHORED;

			if( Array::IndexOf( options, "ANCHOR_START" ) >= 0 )
			{
				mData->mAnchor = RE2::Anchor::ANCHOR_START;
			}
			else if( Array::IndexOf( options, "ANCHOR_BOTH" ) >= 0 )
			{
				mData->mAnchor = RE2::Anchor::ANCHOR_BOTH;
			}
		}
		catch( const std::exception & exc )
		{
			String^ what = gcnew String( exc.what( ) );
			throw gcnew Exception( "Error: " + what );
		}
		catch( Exception^ )
		{
			throw;
		}
		catch( ... )
		{
			throw gcnew Exception( L"Unknown error.\r\n" __FILE__ );
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


	String^ Matcher::GetRe2Version( )
	{
		return L"2020-03-03"; // TODO: use something from sources
	}


	RegexMatches^ Matcher::Matches( String^ text0 )
	{
		try
		{
			OriginalText = text0;

			auto matches = gcnew List<IMatch^>( );

			std::vector<char> text; // (utf-8)
			std::vector<int> indices; // 

			ToUtf8( &text, &indices, text0 );

			re2::StringPiece const full_text( text.data( ), text.size( ) );

			int number_of_capturing_groups = mData->mRe->NumberOfCapturingGroups( );

			std::vector<re2::StringPiece> found_groups;
			found_groups.resize( number_of_capturing_groups + 1 ); // include main match

			const std::map<int, std::string>& group_names = mData->mRe->CapturingGroupNames( );

			int start_pos = 0;
			int previous_start_pos = 0;

			while( mData->mRe->Match(
				full_text,
				start_pos,
				full_text.size( ) - 1,
				mData->mAnchor,
				found_groups.data( ),
				CheckedCast::ToInt32( found_groups.size( ) ) )
				)
			{
				auto match = CreateMatch( found_groups, group_names, text, indices );
				matches->Add( match );

				const re2::StringPiece& main_group = found_groups.front( );

				// advance to the end of found match

				start_pos = CheckedCast::ToInt32( main_group.data( ) + main_group.size( ) - full_text.data( ) );

				if( start_pos == previous_start_pos ) // was empty match
				{
					Debug::Assert( main_group.size( ) == 0 );

					// advance by the size of current utf-8 element

					do { ++start_pos; } while( start_pos < indices.size( ) && indices.at( start_pos ) < 0 );
				}

				if( start_pos > full_text.size( ) ) break; // end of matches

				previous_start_pos = start_pos;
			}

			return gcnew RegexMatches( matches->Count, matches );
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
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}


	String^ Matcher::GetText( int index, int length )
	{
		return OriginalText->Substring( index, length );
	}


	IMatch^ Matcher::CreateMatch( const std::vector<re2::StringPiece>& foundGroups, const std::map<int, std::string>& groupNames,
		const std::vector<char>& text, const std::vector<int>& indices )
	{
		const re2::StringPiece& main_group = foundGroups.front( );

		int utf8index = CheckedCast::ToInt32( main_group.data( ) - text.data( ) );
		int index = indices.at( utf8index );
		if( index < 0 )
		{
			throw gcnew Exception( "Index error (A)." );
		}

		int next_index = indices.at( utf8index + main_group.size( ) );
		if( next_index < 0 )
		{
			// for example, '\C' in pattern -- match one byte
			// TODO: find a more appropriate error text
			throw gcnew Exception( "Index error (B)." );
		}

		int length = next_index - index;

		auto match = SimpleMatch::Create( index, length, this );
		// default group
		match->AddGroup( index, length, true, L"0" );

		// groups
		for( int i = 1; i < foundGroups.size( ); ++i )
		{
			const re2::StringPiece& g = foundGroups[i];

			String^ group_name;
			auto f = groupNames.find( i );
			if( f != groupNames.cend( ) )
			{
				group_name = gcnew String( f->second.c_str( ) );
			}
			else
			{
				group_name = i.ToString( System::Globalization::CultureInfo::InvariantCulture );
			}

			if( g.data( ) == nullptr ) // failed group
			{
				match->AddGroup( 0, 0, false, group_name );
			}
			else
			{
				int utf8index = CheckedCast::ToInt32( g.data( ) - text.data( ) );
				int index = indices.at( utf8index );
				if( index < 0 )
				{
					// for example, '\C' in pattern -- match one byte
					// TODO: find a more appropriate error text
					throw gcnew Exception( "Index error (C)." );
				}

				int next_index = indices.at( utf8index + g.size( ) );
				if( next_index < 0 )
				{
					throw gcnew Exception( "Index error (D)." );
				}

				match->AddGroup( index, next_index - index, true, group_name );
			}
		}

		return match;
	}


	void Matcher::BuildOptions( )
	{

#define C(f, v, n) \
	list->Add(gcnew OptionInfo( gcnew String(#f), gcnew String(n), v)); \
	mOptionSetters[L#f] = &RE2::Options::set_##f;

		List<OptionInfo^>^ list = gcnew List<OptionInfo^>( );

		C( posix_syntax, false, "restrict regexps to POSIX egrep syntax" );
		C( longest_match, false, "search for longest match, not first match" );
		C( literal, false, "interpret string as literal, not regexp" );
		C( never_nl, false, "never match \\n, even if it is in regexp" );
		C( dot_nl, false, "dot matches everything including new line" );
		C( never_capture, false, "parse all parens as non-capturing" );
		C( case_sensitive, true, "match is case-sensitive (regexp can override with (?i) unless in posix_syntax mode)" );
		C( perl_classes, false, "allow Perl's \\d \\s \\w \\D \\S \\W" );
		C( word_boundary, false, "allow Perl's \\b \\B (word boundary and not)" );
		C( one_line, false, "^ and $ only match beginning and end of text" );

		mOptions = list;

#undef C
	}
}
