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


namespace PythonRegexEngineNs
{
	class Matcher : IMatcher, ISimpleTextGetter
	{
		static string PythonVersion = null;
		static readonly object Locker = new object( );
		static readonly Regex RegexMG =
			new Regex( @"^(?'t'[MG]) (?'s'-?\d+), (?'e'-?\d+)|(?'t'N) (?'i'\d+) <(?'n'.*)>$",
				RegexOptions.Compiled | RegexOptions.ExplicitCapture );

		readonly string Pattern;
		readonly string[] SelectedOptions;
		string Text;
		static readonly List<FlagInfo> FlagInfoList;


		public class FlagInfo
		{
			public readonly string Flag;
			public readonly string Note;

			public FlagInfo( string flag, string note )
			{
				Flag = flag;
				Note = note;
			}
		}


		static Matcher( )
		{
			FlagInfoList = new List<FlagInfo>
			{
				new FlagInfo( "ASCII", @"Make \w, \W, \b, \B, \d, \D, \s and \S perform ASCII-only matching"),
				new FlagInfo( "IGNORECASE", @"Perform case-insensitive matching"),
				new FlagInfo( "LOCALE", @"Make \w, \W, \b, \B and case-insensitive matching dependent on the current locale"),
				new FlagInfo( "MULTILINE", @"When specified, the pattern character '^' matches at the beginning of the string and at the beginning of each line (immediately following each newline); and the pattern character '$' matches at the end of the string and at the end of each line (immediately preceding each newline)"),
				new FlagInfo( "DOTALL", @"Make the '.' special character match any character at all, including a newline"),
				new FlagInfo( "VERBOSE", @"Allow spaces and comments"),
			};
		}


		internal Matcher( string pattern, string[] selectedOptions )
		{
			Pattern = pattern;
			SelectedOptions = selectedOptions;
		}


		internal static string GetPythonVersion( )
		{
			if( PythonVersion == null )
			{
				lock( Locker )
				{
					if( PythonVersion == null )
					{
						string stdout_contents;
						string stderr_contents;

						if( !ProcessUtilities.InvokeExe( NonCancellable.Instance, GetPythonExePath( ), @"-V", "", out stdout_contents, out stderr_contents ) )
						{
							if( Debugger.IsAttached ) Debugger.Break( );
							Debug.WriteLine( "Unknown Python version: '{0}' '{1}", stdout_contents, stderr_contents );
							PythonVersion = "unknown version";
						}
						else
						{
							stdout_contents = stdout_contents.Trim( );

							string v = Regex.Match( stdout_contents, @"^Python (\d+(\.\d+)*)" ).Groups[1].Value;

							if( string.IsNullOrWhiteSpace( v ) )
							{
								if( Debugger.IsAttached ) Debugger.Break( );
								Debug.WriteLine( "Unknown Python version: '{0}' '{1}", stdout_contents, stderr_contents );
								PythonVersion = "unknown version";
							}
							else
							{
								PythonVersion = v;
							}
						}
					}
				}
			}

			return PythonVersion;
		}


		#region IMatcher

		public RegexMatches Matches( string text, ICancellable cnc )
		{
			// TODO: optimise, redesign

			Text = text;

			var all_flags = FlagInfoList.Select( oi => oi.Flag );
			var selected_flags = SelectedOptions?.Where( o => all_flags.Contains( o ) ) ?? Enumerable.Empty<string>( );


			var matches = new List<IMatch>( );

			string arguments = @"-I -E -s -S -X utf8 -c ""
import sys
import re

pattern = input().strip(' \r\n')
text = input().strip(' \r\n')

pattern = pattern.replace('\\\\', '\x1F').replace('\\r', '\r').replace('\\n', '\n').replace('\x1F', '\\')
text = text.replace('\\\\', '\x1F').replace('\\r', '\r').replace('\\n', '\n').replace('\x1F', '\\')

pattern = pattern[1:-1]
text = text[1:-1]

#print( f'# pattern=[{pattern}], len={len(pattern)}');
#print( f'# text=[{text}], len={len(text)}');

try:

	regex = re.compile( pattern, [*FLAGS*])

	#print( f'# {regex.groups}')
	#print( f'# {regex.groupindex}')

	for key, value in regex.groupindex.items():
		print( f'N {value} <{key}>')

	matches = regex.finditer( text )

	for match in matches :
		print( f'M {match.start()}, {match.end()}')
		for g in range(0, regex.groups + 1):
			print( f'G {match.start(g)}, {match.end(g)}' )

except:
	ex_type, ex, tb = sys.exc_info()

	print( ex, file = sys.stderr )

""
";

			arguments = arguments.Replace( "[*FLAGS*]",
				selected_flags.Any( ) ? string.Join( "|", selected_flags.Select( f => "re." + f ) ) : "0" );

			string stdout_contents;
			string stderr_contents;

			if( !ProcessUtilities.InvokeExe( cnc, GetPythonExePath( ), arguments,
				sw =>
				{
					sw.WriteLine( PrepareString( Pattern ) );
					sw.WriteLine( PrepareString( text ) );
				}, out stdout_contents, out stderr_contents ) )
			{
				return RegexMatches.Empty;
			}

			SimpleMatch match = null;
			int group_i = 0;
			var names = new Dictionary<int, string>( );
			var sph = new SurrogatePairsHelper( text, processSurrogatePairs: true );

			using( var sr = new StringReader( stdout_contents ) )
			{
				string line;

				while( ( line = sr.ReadLine( ) ) != null )
				{
					if( line.Length == 0 || line.StartsWith( "#" ) ) continue;

					var m = RegexMG.Match( line );

					if( !m.Success )
					{
						if( Debugger.IsAttached ) Debugger.Break( );

						throw new Exception( "Internal error in Python engine." );
					}
					else
					{
						switch( m.Groups["t"].Value )
						{
						case "N":
							{
								int index = int.Parse( m.Groups["i"].Value, CultureInfo.InvariantCulture );
								string name = m.Groups["n"].Value;

								Debug.Assert( !names.ContainsKey( index ) );

								names[index] = name;
							}
							break;
						case "M":
							{
								int index = int.Parse( m.Groups["s"].Value, CultureInfo.InvariantCulture );
								int end = int.Parse( m.Groups["e"].Value, CultureInfo.InvariantCulture );
								int length = end - index;

								Debug.Assert( index >= 0 && end >= 0 );

								var (text_index, text_length) = sph.ToTextIndexAndLength( index, length );

								match = SimpleMatch.Create( index, length, text_index, text_length, this );
								matches.Add( match );

								group_i = 0;
							}
							break;
						case "G":
							{
								int index = int.Parse( m.Groups["s"].Value, CultureInfo.InvariantCulture );
								int end = int.Parse( m.Groups["e"].Value, CultureInfo.InvariantCulture );
								int length = end - index;
								bool success = index >= 0;

								Debug.Assert( match != null );

								var (text_index, text_length) = sph.ToTextIndexAndLength( index, length );

								string name;
								if( !names.TryGetValue( group_i, out name ) ) name = group_i.ToString( CultureInfo.InvariantCulture );

								match.AddGroup( index, length, text_index, text_length, success, name );

								++group_i;
							}
							break;
						default:
							if( Debugger.IsAttached ) Debugger.Break( );

							throw new Exception( "Internal error in Python engine." );
						}
					}
				}
			}


			return new RegexMatches( matches.Count, matches );
		}

		#endregion IMatcher

		#region ISimpleTextGetter

		public string GetText( int index, int length )
		{
			return Text.Substring( index, length );
		}

		#endregion ISimpleTextGetter


		public static IReadOnlyList<FlagInfo> GetOptionInfoList( ) => FlagInfoList;


		static string GetPythonExePath( )
		{
			string assembly_location = Assembly.GetExecutingAssembly( ).Location;
			string assembly_dir = Path.GetDirectoryName( assembly_location );
			string python_dir = Path.Combine( assembly_dir, @"Python-embed" );
			string python_exe = Path.Combine( python_dir, @"python.exe" );

			return python_exe;
		}

		static string PrepareString( string text )
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