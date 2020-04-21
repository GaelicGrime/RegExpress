#include "pch.h"
#include "Matcher.h"


using namespace System::Reflection;
using namespace System::IO;
using namespace System::Runtime::InteropServices;
using namespace System::Text;

using namespace msclr::interop;


namespace Perl5RegexInterop
{

	static PerlInterpreter* CreatePerlInterpreter( )
	{
		marshal_context mc{};

		int argc = 2;
		char* argvd[3] = { "", NULL };
		char** argv = argvd;

		char* envd[1] = { NULL };
		char** env = envd;

		String^ p = Assembly::GetExecutingAssembly( )->Location;
		p = Path::GetDirectoryName( p );
		p = Path::Combine( p, "Perl-min\\perl\\lib" );
		const char* perl_include = mc.marshal_as<const char*>( p );

		char* embedding[] = { "", "-CSA", "-I", const_cast<char*>( perl_include ), "-e", "0", NULL };
		const int embedding_size_no_null = _countof( embedding ) - 1;

		static bool in = false;
		if( !in ) 
		{
			in = true; //.................
			PERL_SYS_INIT3( &argc, &argv, &env );
		}

		PerlInterpreter* my_perl = perl_alloc( );
		perl_construct( my_perl );
		perl_parse( my_perl, NULL, embedding_size_no_null, embedding, NULL );
		//...PL_exit_flags |= PERL_EXIT_DESTRUCT_END; //?
		//...perl_run( my_perl );	//?

		return my_perl;
	}


	static void ClosePerlInterpreter( PerlInterpreter* my_perl )
	{
		if( my_perl != nullptr )
		{
			perl_destruct( my_perl );
			perl_free( my_perl );
			//...PERL_SYS_TERM( ); //? 
		}
	}


	static Matcher::Matcher( )
	{
		PerlLocker = gcnew Object( );
	}


	Matcher::Matcher( String^ pattern, cli::array<String^>^ options )
		: Pattern( pattern )
	{
	}


	Matcher::~Matcher( )
	{
		this->!Matcher( );
	}


	Matcher::!Matcher( )
	{
	}


	String^ Matcher::GetVersion( )
	{
		if( PerlVersion == nullptr )
		{
			msclr::lock lk( PerlLocker );

			if( PerlVersion == nullptr )
			{
				auto my_perl = CreatePerlInterpreter( );
				try
				{
					SV* sv = eval_pv( "$^V;", 0 );

					if( sv == nullptr || !SvOK( sv ) )
					{
						PerlVersion = "(unknown version)";
					}
					else
					{
						const char* version_string = SvPV_nolen( sv );
						PerlVersion = gcnew String( version_string );
					}

					SvREFCNT_dec( sv );
				}
				finally
				{
					ClosePerlInterpreter( my_perl );
				}
			}
		}

		return PerlVersion;
	}


	String^ ToUtf8PerlString( String^ text )
	{
		auto sb = gcnew StringBuilder( "\"" );

		auto bytes = Encoding::UTF8->GetBytes( text );

		for each( byte b in bytes )
		{
			switch( b )
			{
			case '\\':
				sb->Append( "\\\\" );
				break;
			case '"':
				sb->Append( "\\\"" );
				break;
			case '\n':
				sb->Append( "\\n" );
				break;
			case '\r':
				sb->Append( "\\r" );
				break;
			case '\t':
				sb->Append( "\\t" );
				break;
			default:
				if( b >= 0x20 && b <= 0x7E )
				{
					sb->Append( (wchar_t)b );
				}
				else
				{
					sb->Append( "\\x" )->Append( b.ToString( "X2" ) );
				}
			}
		}

		return sb->Append( "\"" )->ToString( );
	}


	RegexMatches^ Matcher::Matches( String^ text )
	{
		try
		{
			marshal_context mc{};

			OriginalText = text;
			auto matches = gcnew List<IMatch^>;

			String^ perl_code =
				R"VERBATIM(
eval 
{
	local $SIG{__DIE__}; 

	use feature 'unicode_strings'; 
	use utf8;

	$p = {0};					   
	$t = {1};
	utf8::decode($p);
	utf8::decode($t);

	@matches = ( );

	while( $t =~ /$p/g )
	{
		for( my $i = 0; $i < scalar @+; ++$i)
		{
			my $m = '';
			my $success = defined @-[$i]; 
			if( ! $success )
			{
		        $m = '0|0|0';
			}
			else
			{
				my $index = @-[$i];
				my $length = @+[$i] - @-[$i];
		        $m = "1|$index|$length";
			}
			push @matches, $m;
		}
	}
};
 
$err = $@;

)VERBATIM";
			// TODO: optimise
			perl_code = perl_code
				->Replace( "{0}", ToUtf8PerlString( Pattern ) )
				->Replace( "{1}", ToUtf8PerlString( text ) );

			const char* native_perl_code = mc.marshal_as<const char*>( perl_code );

			msclr::lock lk( PerlLocker );

			auto my_perl = CreatePerlInterpreter( );

			try
			{

				SV* sv;

#ifdef _DEBUG
				sv = eval_pv( native_perl_code, 1 );
#else
				sv = eval_pv( native_perl_code, 0 );
#endif

				SV* sv_err = get_sv( "err", 0 );
				if( sv_err == nullptr )
				{
					//SvREFCNT_dec( sv_err );
					//SvREFCNT_dec( sv );

					throw gcnew ApplicationException( "Something went wrong in Perl interpreter." );
				}

				String^ err = gcnew String( SvPV_nolen( sv_err ) );

				//SvREFCNT_dec( sv_err );
				//SvREFCNT_dec( sv );

				if( !String::IsNullOrWhiteSpace( err ) )
				{
					throw gcnew ApplicationException( String::Format( "Perl error: '{0}'.", err ) );
				}

				AV* av_matches = get_av( "matches", 0 );
				int total_matches = av_top_index( av_matches ) + 1;

				for( int i = 0; i < total_matches; ++i )
				{
					SV** sv_match = av_fetch( av_matches, i, 0 );

					//if( SvTYPE( SvRV( *match_sv ) ) != SVt_PVAV ) throw gcnew ApplicationException( "Something went wrong in Perl interpreter." );

					//AV* match_av = (AV*)SvRV( *match_sv );

					//SV** sv = av_fetch( match_av, 0, 0 );
					//auto success = SvUV( *sv );

					//sv = av_fetch( match_av, 1, 0 );
					//auto index = SvUV( *sv );

					//sv = av_fetch( match_av, 2, 0 );
					//auto length = SvUV( *sv );


					String^ m = gcnew String( SvPV_nolen( *sv_match ) );
					auto spl = m->Split( L'|' );
					bool success = spl[0] == "1";
					if( success )
					{
						int index = int::Parse( spl[1] );
						int length = int::Parse( spl[2] );

						auto match = SimpleMatch::Create( index, length, this );
						matches->Add( match );
					}
				}

				//SvREFCNT_dec( av_matches );


				//auto b0 = SvOK( r ); // defined
				//auto b1 = SvIOK( r );
				//auto b2 = SvUOK( r );
				//auto b3 = SvNOK( r );
				//auto b4 = SvPOK( r );
				//b1 = SvIOKp( r );
				////b2 = SvUOKp( sv );
				//b3 = SvNOKp( r );
				//b4 = SvPOKp( r );
				////PL_errors

				//String^ e;
				////if( b4 )
				//{
				//	const char* e0 = SvPV_nolen( get_sv( "e", 0 ) );
				//	e = gcnew String( e0 );
				//}

			}
			finally
			{
				ClosePerlInterpreter( my_perl );
			}

			REGEXP* rx;	 //...

			return gcnew RegexMatches( matches->Count, matches );
		}
		catch( Exception^ exc )
		{
			//UNREFERENCED_PARAMETER( exc );
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
}
