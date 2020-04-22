using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.Matches.Simple;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;


namespace Perl5RegexEngineNs
{
	class Matcher : IMatcher, ISimpleTextGetter
	{
		readonly string Pattern;
		readonly string[] SelectedOptions;
		string Text;
		static readonly List<OptionInfo> OptionInfoList;

		public class OptionInfo
		{
			public readonly string Modifier;
			public readonly string Note;

			public OptionInfo( string modifier, string note )
			{
				Modifier = modifier;
				Note = note;
			}
		}


		static Matcher( )
		{
			OptionInfoList = new List<OptionInfo>
			{
				new OptionInfo("m", "change \"^\" and \"$\" to matching the start and end of each line within the string"),
				new OptionInfo("s", "change \".\" to match any character whatsoever, even a newline"),
				new OptionInfo("i", "do case-insensitive pattern matching"),
				new OptionInfo("x", "permitting whitespace and comments"),
				new OptionInfo("xx", "like \"x\", but additionally ignore spaces within [ ]"),
				new OptionInfo("n", "prevent the grouping metacharacters ( ) from capturing"),
				new OptionInfo("a", "ASCII-restrict"),
				new OptionInfo("aa", "forbid ASCII/non-ASCII matches"),
				new OptionInfo("d", "old, problematic default character set behavior"),
				new OptionInfo("u", "use Unicode rules"),
				new OptionInfo("l", "use the current locale's rules"),
			};
		}


		public Matcher( string pattern, string[] selected_options )
		{
			Pattern = pattern;
			SelectedOptions = selected_options;
		}


		#region IMatcher

		public RegexMatches Matches( string text )
		{
			Text = text;

			var matches = new List<IMatch>( );

			string assembly_location = Assembly.GetExecutingAssembly( ).Location;
			string assembly_dir = Path.GetDirectoryName( assembly_location );
			string perl_dir = Path.Combine( assembly_dir, @"Perl5-min\perl" );
			string perl_exe = Path.Combine( perl_dir, @"bin\perl.exe" );

			var psi = new ProcessStartInfo( );

			psi.FileName = perl_exe;
			psi.Arguments = @"-CS -e ""
eval
{
use strict; 
use feature 'unicode_strings';
use utf8;

chomp( my $pattern = <STDIN> ); 
chomp( my $text = <STDIN> ); 
chomp( my $options = <STDIN> ); 

print q('), $pattern, q(' ), length $pattern, qq(\n);

$pattern = substr $pattern, 1, length($pattern) - 2;
$text = substr $text, 1, length($text) - 2;

print q('), $pattern, q(' ), length $pattern, qq(\n);

utf8::decode( $pattern );
utf8::decode( $text );
utf8::decode( $options );

print $pattern, ' ', length $pattern, qq(\n);
print $text, ' ', length $text, q(\n);
print '<START>';

# TODO: unescape strings


while ($text =~ /$pattern/g{OPTIONS}) 
{
	for( my $i = 0; $i < scalar @+; ++$i)
	{
		my $success = defined @-[$i]; 
		if( ! $success )
		{
			print '0|0|0';
		}
		else
		{
			my $index = @-[$i];
			my $length = @+[$i] - @-[$i];
			#my $val = @{^CAPTURE}[$i];
			
			print qq(1|$index|$length);
		}

		print 'G';
	}

	print 'M';

}

};

if( $@ )
{
	print STDERR $@, qq(\n);
}
"""
.Replace( "{OPTIONS}", string.Concat( SelectedOptions ?? new string[] { } ) );

			psi.UseShellExecute = false;
			psi.RedirectStandardInput = true;
			psi.RedirectStandardOutput = true;
			psi.RedirectStandardError = true;
			psi.StandardOutputEncoding = Encoding.UTF8;
			psi.StandardErrorEncoding = Encoding.UTF8;
			psi.CreateNoWindow = true;
			psi.WindowStyle = ProcessWindowStyle.Hidden;

			string output;
			string error;

			using( Process p = Process.Start( psi ) )
			{
				using( StreamWriter sw = new StreamWriter( p.StandardInput.BaseStream, new UTF8Encoding( encoderShouldEmitUTF8Identifier: false ) ) )
				//using( StreamWriter sw = new StreamWriter( p.StandardInput.BaseStream, Encoding.ASCII ) )
				{
					sw.WriteLine( EscapeString( Pattern ) );
					sw.WriteLine( EscapeString( text ) );
					sw.WriteLine( EscapeString( "options..." ) );
				}

				output = p.StandardOutput.ReadToEnd( );
				error = p.StandardError.ReadToEnd( );
			}

			if( output.StartsWith( "ERROR: " ) )
			{
				if( !string.IsNullOrEmpty( error ) ) error += Environment.NewLine;
				error += output.Substring( "ERROR: ".Length );
			}

			if( !string.IsNullOrWhiteSpace( error ) )
			{
				throw new Exception( "Perl error: " + error );
			}

			// TODO: optimise, redesign

			int start = output.IndexOf( "<START>" );
			if( start >= 0 ) output = output.Substring( start + "<START>".Length );

			output = output.TrimStart( );

			var split_m = output.Split( new[] { 'M' }, StringSplitOptions.RemoveEmptyEntries );
			foreach( var m in split_m )
			{
				SimpleMatch match = null;

				var split_g = m.Split( new[] { 'G' }, StringSplitOptions.RemoveEmptyEntries );

				for( int i = 0; i < split_g.Length; i++ )
				{
					string g = split_g[i];
					var split = g.Split( '|' );
					Debug.Assert( split.Length == 3 );

					bool success = split[0] == "1";

					if( !success )
					{
						match.AddGroup( 0, 0, false, i.ToString( CultureInfo.InvariantCulture ) );
					}
					else
					{
						int index = int.Parse( split[1], CultureInfo.InvariantCulture );
						int length = int.Parse( split[2], CultureInfo.InvariantCulture );

						if( match == null ) match = SimpleMatch.Create( index, length, this );

						match.AddGroup( index, length, true, i.ToString( CultureInfo.InvariantCulture ) );
					}
				}

				matches.Add( match );
			}

			return new RegexMatches( matches.Count, matches );
		}

		#endregion IMatcher

		#region ISimpleTextGettetr

		public string GetText( int index, int length )
		{
			return Text.Substring( index, length );
		}

		#endregion


		public static IReadOnlyList<OptionInfo> GetOptionInfoList( ) => OptionInfoList;


		string EscapeString1( string text ) //.......
		{
			var sb = new StringBuilder( "[" );

			var bytes = Encoding.UTF8.GetBytes( text );

			foreach( byte b in bytes )
			{
				switch( b )
				{
				case unchecked((byte)'\\'):
					sb.Append( "\\\\" );
					break;
				case unchecked((byte)'\n'):
					sb.Append( "\\n" );
					break;
				case unchecked((byte)'\r'):
					sb.Append( "\\r" );
					break;
				default:

					sb.Append( (char)b );
					break;
				}
			}

			return sb.Append( ']' ).ToString( );
		}


		string EscapeString( string text )
		{
			var sb = new StringBuilder( "[" );

			foreach( char c in text )
			{
				switch( c )
				{
				case '\\':
					sb.Append( "\\\\" );
					break;
				case '\n':
					sb.Append( "\\n" );
					break;
				case '\r':
					sb.Append( "\\r" );
					break;
				default:
					sb.Append( c );
					break;
				}
			}

			return sb.Append( ']' ).ToString( );
		}



	}
}
