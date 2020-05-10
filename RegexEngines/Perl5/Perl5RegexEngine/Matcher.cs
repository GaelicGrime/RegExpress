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


		public Matcher( string pattern, string[] selectedOptions )
		{
			Pattern = pattern;
			SelectedOptions = selectedOptions;
		}


		#region IMatcher

		public RegexMatches Matches( string text, ICancellable cnc )
		{
			// TODO: optimise, redesign

			Text = text;

			var all_modifiers = ModifierInfoList.Select( oi => oi.Modifier );
			string selected_modifiers = SelectedOptions == null ? "" : string.Concat( SelectedOptions.Where( o => all_modifiers.Contains( o ) ) );

			var matches = new List<IMatch>( );

			string assembly_location = Assembly.GetExecutingAssembly( ).Location;
			string assembly_dir = Path.GetDirectoryName( assembly_location );
			string perl_dir = Path.Combine( assembly_dir, @"Perl5-min\perl" );
			string perl_exe = Path.Combine( perl_dir, @"bin\perl.exe" );

			string arguments = @"-CS -e ""
my $pattern;
eval
{
	use strict; 
	use feature 'unicode_strings';
	use utf8;
	#use re 'eval';
	no warnings 'experimental::re_strict';
	[*USE RE STRICT*]

	chomp( $pattern = <STDIN> ); 
	chomp( my $text = <STDIN> ); 

	#print q('), $pattern, q(' ), length $pattern, qq(\n);

	$pattern = substr $pattern, 1, length($pattern) - 2;
	$text = substr $text, 1, length($text) - 2;

	#print q('), $pattern, q(' ), length $pattern, qq(\n);

	utf8::decode( $pattern );
	utf8::decode( $text );

	$pattern =~ s/\\\\/\x1F/g;
	$pattern =~ s/\\n/\n/g;
	$pattern =~ s/\\r/\r/g;
	$pattern =~ s/\x1F/\\/g;

	$text =~ s/\\\\/\x1F/g;
	$text =~ s/\\n/\n/g;
	$text =~ s/\\r/\r/g;
	$text =~ s/\x1F/\\/g;

	#print 'pattern: ', q('), $pattern, q(' ), length $pattern, qq(\r\n);
	#print 'text: ', q('), $text, ' ', q(' ), length $text, qq(\r\n);
	
	my $re;
	do 
	{
		use re qw(Debug PARSE);

		print STDERR qq(<DEBUG-PARSE\x1F>\n);

		$re = qr/$pattern/[*MODIFIERS*];

		print STDERR qq(</DEBUG-PARSE\x1F>\n);
	};

	my $results = qq(<RESULTS\x1F>);

	while( $text =~ /$re/g ) 
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

	$results .= qq(</RESULTS\x1F>);

	print $results;

};

if( $@ )
{
	print STDERR qq(<ERR\x1F>), $@, qq(</ERR\x1F>\n);
}

print STDERR qq(<END-ERR\x1F/>\n);

"""
.Replace( "[*MODIFIERS*]", selected_modifiers )
.Replace( "[*USE RE STRICT*]", SelectedOptions.Contains( "strict" ) ? "use re 'strict';" : "" );

			var output_sb = new StringBuilder( );
			var error_sb = new StringBuilder( );

			using( Process p = new Process( ) )
			{
				p.StartInfo.FileName = perl_exe;
				p.StartInfo.Arguments = arguments;

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
					output_sb.AppendLine( a.Data );
				};

				p.ErrorDataReceived += ( s, a ) =>
				{
					error_sb.AppendLine( a.Data );
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

				bool cancel = false;
				bool done = false;

				for(; ; )
				{
					cancel = cnc.IsCancellationRequested;
					if( cancel ) break;

					done = p.WaitForExit( 444 );
					if( done )
					{
						// another 'WaitForExit' required to finish the processing of streams;
						// see: https://stackoverflow.com/questions/9533070/how-to-read-to-end-process-output-asynchronously-in-c,
						// https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit

						p.WaitForExit( );

						break;
					}
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
			string debug_parse = Regex.Match( error, @"<DEBUG-PARSE\x1F>(.*?)</DEBUG-PARSE\x1F>", RegexOptions.Singleline | RegexOptions.Compiled ).Groups[1].Value.Trim( );
			string error_text = Regex.Match( error, @"<ERR\x1F>(.*?)</ERR\x1F>", RegexOptions.Singleline | RegexOptions.Compiled ).Groups[1].Value.Trim( );

			if( !string.IsNullOrWhiteSpace( error_text ) )
			{
				string error_message = Regex.Replace( error_text, @"\s+at -e line \d+, <STDIN> line \d+(?=\.\s*$)", "", RegexOptions.Singleline | RegexOptions.Compiled );

				throw new Exception( $"Perl error: {error_message}" );
			}

			// try figuring out the names and their numbers

			var numbered_names = new List<string>( );

			foreach( Match m in Regex.Matches( debug_parse, @"(?:\r|\n) +\| +\| +~ CLOSE(\d+) '(.*?)' \(\d+\)(?: -> \w+)?(?:\r|\n)", RegexOptions.Compiled ) )
			{
				string name = m.Groups[2].Value;
				int number = int.Parse( m.Groups[1].Value, CultureInfo.InvariantCulture );

				for( int i = numbered_names.Count; i <= number; ++i ) numbered_names.Add( null );

				Debug.Assert( numbered_names[number] == null || numbered_names[number] == name );

				numbered_names[number] = name;
			}

			string output = output_sb.ToString( );

			string results = Regex.Match( output, @"<RESULTS\x1F>(.*?)</RESULTS\x1F>", RegexOptions.Singleline | RegexOptions.Compiled ).Groups[1].Value.Trim( );

			var sph = new SurrogatePairsHelper( text, processSurrogatePairs: true );
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

					string deduced_name = i < numbered_names.Count ? numbered_names[i] : null;
					if( deduced_name == null ) deduced_name = i.ToString( CultureInfo.InvariantCulture );

					if( !success )
					{
						match.AddGroup( 0, 0, false, deduced_name );
					}
					else
					{
						int index = int.Parse( split[1], CultureInfo.InvariantCulture );
						int length = int.Parse( split[2], CultureInfo.InvariantCulture );
						var (text_index, text_length) = sph.ToTextIndexAndLength( index, length );

						if( match == null ) match = SimpleMatch.Create( index, length, text_index, text_length, this );

						match.AddGroup( index, length, text_index, text_length, true, deduced_name );
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
