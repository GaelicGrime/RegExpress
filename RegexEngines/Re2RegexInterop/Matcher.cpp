#include "pch.h"

#include "Matcher.h"
#include "Group.h"
#include "Match.h"


using namespace System::Diagnostics;

using namespace msclr::interop;


namespace Re2RegexInterop
{

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

		utf8.resize( bytes_written + 1, 0 ); // (zero-terminated)

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
			( *indices )[bytes_written] = p - start;

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

		dest->resize( bytes_written + 1, 0 ); // (zero-terminated)
		indices->resize( bytes_written );
		indices->push_back( s->Length );

		setlocale( LC_ALL, old_locale ); // restore
	}


	Matcher::Matcher( String^ pattern0, cli::array<String^>^ options )
		: mData( nullptr )
	{
		try
		{
			RE2::Options options{}; // TODO: implement

			std::vector<char> utf8 = ToUtf8( pattern0 );

			Debug::Assert( utf8.size( ) > 0 );

			re2::StringPiece pattern( utf8.data( ), utf8.size( ) - 1 ); // (zero-terminator excluded)

			std::unique_ptr<RE2> re( new RE2( pattern, options ) );

			if( !re->ok( ) )
			{
				throw gcnew Exception( String::Format( L"RE2 Error {0}: {1}", (int)re->error_code( ), gcnew String( re->error( ).c_str( ) ) ) );
			}

			mData = new MatcherData{};

			mData->mRe = std::move( re );
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
		return L"2020-01-01"; // TODO: use something from sources
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

			const auto& group_names = mData->mRe->CapturingGroupNames( ); // a 'map<int, string>'

			int start_pos = 0;
			int previous_start_pos = 0;

			while( mData->mRe->Match(
				full_text,
				start_pos,
				full_text.size( ) - 1,
				RE2::Anchor::UNANCHORED,
				found_groups.data( ),
				found_groups.size( ) )
				)
			{
				const re2::StringPiece& main_group = found_groups.front( );

				int utf8index = main_group.data( ) - text.data( );
				int index = indices.at( utf8index );
				if( index < 0 )
				{
					throw gcnew Exception( "Index error." );
				}

				Match^ match = gcnew Match( this, index, main_group.size( ) );

				// default group
				match->AddGroup( gcnew Group( match, "0", true, index, main_group.size( ) ) );

				// groups
				for( int i = 1; i < found_groups.size( ); ++i )
				{
					const re2::StringPiece& g = found_groups[i];

					String^ group_name;
					auto f = group_names.find( i );
					if( f != group_names.cend( ) )
					{
						group_name = gcnew String( f->second.c_str( ) );
					}
					else
					{
						group_name = i.ToString( System::Globalization::CultureInfo::InvariantCulture );
					}

					Group^ group;

					if( g.data( ) == nullptr ) // failed group
					{
						group = gcnew Group( match, group_name, false, 0, 0 );
					}
					else
					{
						int utf8index = g.data( ) - text.data( );
						int index = indices.at( utf8index );
						if( index < 0 )
						{
							throw gcnew Exception( "Index error." );
						}

						group = gcnew Group( match, group_name, true, index, g.size( ) );
					}

					match->AddGroup( group );
				}

				matches->Add( match );

				// advance to the end of found match

				start_pos = main_group.data( ) + main_group.size( ) - full_text.data( );

				if( start_pos == previous_start_pos ) // was empty match
				{
					Debug::Assert( main_group.size( ) == 0 );

					// advance by the size of current utf-8 element

					do { ++start_pos; } while( start_pos < indices.size() && indices.at( start_pos ) < 0 );
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
			throw exc;
		}
		catch( ... )
		{
			// TODO: also catch 'boost::exception'?
			throw gcnew Exception( "Unknown error.\r\n" __FILE__ );
		}
	}


	void Matcher::BuildOptions( )
	{
#define C(f, n) \
	list->Add(gcnew OptionInfo( f, gcnew String(#f), gcnew String(n)));

		List<OptionInfo^>^ list = gcnew List<OptionInfo^>( );

		//C( PCRE2_ANCHORED, "Force pattern anchoring" );

		//mCompileOptions = list;


#undef C
	}
}
