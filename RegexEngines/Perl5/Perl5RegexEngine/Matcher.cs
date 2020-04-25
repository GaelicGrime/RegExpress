using RegexEngineInfrastructure;
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace Perl5RegexEngineNs
{
	class Matcher : IMatcher, ISimpleTextGetter
	{
		readonly string Pattern;
		readonly string[] SelectedOptions;
		string Text;
		static readonly List<ModifierInfo> ModifierInfoList;

		public class ModifierInfo
		{
			public readonly string Modifier;
			public readonly string Note;

			public ModifierInfo( string modifier, string note )
			{
				Modifier = modifier;
				Note = note;
			}
		}


		static Matcher( )
		{
			ModifierInfoList = new List<ModifierInfo>
			{
				new ModifierInfo("m", "change \"^\" and \"$\" to matching the start and end of each line within the string"),
				new ModifierInfo("s", "change \".\" to match any character whatsoever, even a newline"),
				new ModifierInfo("i", "do case-insensitive pattern matching"),
				new ModifierInfo("x", "permitting whitespace and comments"),
				new ModifierInfo("xx", "like \"x\", but additionally ignore spaces within [ ]"),
				new ModifierInfo("n", "prevent the grouping metacharacters ( ) from capturing"),
				new ModifierInfo("a", "ASCII-restrict"),
				new ModifierInfo("aa", "forbid ASCII/non-ASCII matches"),
				new ModifierInfo("d", "old, problematic default character set behavior"),
				new ModifierInfo("u", "use Unicode rules"),
				new ModifierInfo("l", "use the current locale's rules"),
				//?new OptionInfo("c", "keep the current position during repeated matching"),
			};
		}


		public Matcher( string pattern, string[] selected_options )
		{
			Pattern = pattern;
			SelectedOptions = selected_options;
		}


		#region IMatcher

		public RegexMatches Matches( string text, ICancellable cnc )
		{
			Text = text;

			var all_modifiers = ModifierInfoList.Select( oi => oi.Modifier );
			string selected_modifiers = SelectedOptions == null ? "" : string.Concat( SelectedOptions.Where( o => all_modifiers.Contains( o ) ) );

			var matches = new List<IMatch>( );

			string assembly_location = Assembly.GetExecutingAssembly( ).Location;
			string assembly_dir = Path.GetDirectoryName( assembly_location );
			string perl_dir = Path.Combine( assembly_dir, @"Perl5-min\perl" );
			string perl_exe = Path.Combine( perl_dir, @"bin\perl.exe" );

			var output_sb = new StringBuilder( );
			var error_sb = new StringBuilder( );

			using( Process p = new Process( ) )
			{
				p.StartInfo.FileName = perl_exe;
				p.StartInfo.Arguments = @"-CS -e ""
eval
{
	use strict; 
	use feature 'unicode_strings';
	use utf8;
	#use re 'eval';

	chomp( my $pattern = <STDIN> ); 
	chomp( my $text = <STDIN> ); 

	#print q('), $pattern, q(' ), length $pattern, qq(\n);

	$pattern = substr $pattern, 1, length($pattern) - 2;
	$text = substr $text, 1, length($text) - 2;

	#print q('), $pattern, q(' ), length $pattern, qq(\n);

	utf8::decode( $pattern );
	utf8::decode( $text );

	$pattern =~ s/\\n/\n/g;
	$pattern =~ s/\\r/\r/g;
	$pattern =~ s/\\\\/\\/g;

	$text =~ s/\\n/\n/g;
	$text =~ s/\\r/\r/g;
	$text =~ s/\\\\/\\/g;

	#print 'pattern: ', q('), $pattern, q(' ), length $pattern, qq(\r\n);
	#print 'text: ', q('), $text, ' ', q(' ), length $text, qq(\r\n);
	
	my $results = qq(<RESULTS-\x1F>);

	while ($text =~ /$pattern/g[*MODIFIERS*]) 
	{
		for( my $i = 0; $i < scalar @+; ++$i)
		{
			my $success = defined @-[$i]; 
			if( ! $success )
			{
				$results .= '0|0|0';
			}
			else
			{
				my $index = @-[$i];
				my $length = @+[$i] - @-[$i];
				#my $val = @{^CAPTURE}[$i];
			
				$results .= qq(1|$index|$length);
			}

			$results .= 'G';
		}

		$results .= 'M';

	}

	$results .= qq(</RESULTS-\x1F>);

	print $results;

};

if( $@ )
{
	print STDERR $@, qq(\n);
}

"""
.Replace( "[*MODIFIERS*]", selected_modifiers );

				p.StartInfo.UseShellExecute = false;
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

				p.StartInfo.RedirectStandardInput = true;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.RedirectStandardError = true;
				p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
				p.StartInfo.StandardErrorEncoding = Encoding.UTF8;

				p.OutputDataReceived += ( s, a ) =>
				{
					output_sb.Append( a.Data );
				};

				p.ErrorDataReceived += ( s, a ) =>
				{
					error_sb.Append( a.Data );
				};

				p.Start( );
				p.BeginOutputReadLine( );
				p.BeginErrorReadLine( );

				using( StreamWriter sw = new StreamWriter( p.StandardInput.BaseStream, new UTF8Encoding( encoderShouldEmitUTF8Identifier: false ) ) )
				{
					sw.WriteLine( PrepareString( Pattern ) );
					sw.WriteLine( PrepareString( text ) );
				}

				// TODO: use timeout
				// TODO: implement "cancelisation" in more places

				bool done = false;
				bool cancel = false;

				for(; ; )
				{
					done = p.WaitForExit( 444 );
					if( done ) break;

					cancel = cnc.IsCancellationRequested;
					if( cancel ) break;
				}

				if( cancel )
				{
					try
					{
						p.Kill( );
					}
					catch( Exception _ )
					{
						if( Debugger.IsAttached ) Debugger.Break( );

						// ignore
					}

					return new RegexMatches( 0, Enumerable.Empty<IMatch>( ) );
				}

				Debug.Assert( done );
			}

			string error = error_sb.ToString( );

			if( !string.IsNullOrWhiteSpace( error ) )
			{
				string error_message = Regex.Replace( error, @"\s+at -e line \d+, <STDIN> line \d+(?=\.\s*$)", "" );

				throw new Exception( error_message );
			}

			// TODO: optimise, redesign

			string output = output_sb.ToString( );

			string results = Regex.Match( output, @"<RESULTS-\x1F>(.*?)</RESULTS-\x1F>" ).Groups[1].Value.Trim( );

			var split_m = results.Split( new[] { 'M' }, StringSplitOptions.RemoveEmptyEntries );
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


		public static IReadOnlyList<ModifierInfo> GetOptionInfoList( ) => ModifierInfoList;


		string PrepareString( string text )
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
