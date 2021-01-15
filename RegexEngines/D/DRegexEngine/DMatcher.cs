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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


namespace DRegexEngineNs
{
	sealed class DMatcher : IMatcher, ISimpleTextGetter
	{
		readonly DRegexOptions Options;
		readonly string Pattern;
		string Text;


		public DMatcher( string pattern, DRegexOptions options )
		{
			Options = options;
			Pattern = pattern;
		}


		public static string GetDVersion( ICancellable cnc )
		{
			string stdout_contents;
			string stderr_contents;

			if( !InvokeDClient( cnc, "{\"c\":\"v\"}", out stdout_contents, out stderr_contents ) ) return null;

			if( !string.IsNullOrWhiteSpace( stderr_contents ) )
			{
				throw new Exception( stderr_contents );
			}

			var r = JsonSerializer.Deserialize<DClientVersionResponse>( stdout_contents );

			return r.version;
		}


		#region IMatcher

		public RegexMatches Matches( string text, ICancellable cnc )
		{
			Text = text;
			byte[] text_utf8_bytes = Encoding.UTF8.GetBytes( text );

			var flags = new StringBuilder( );

			if( Options.i ) flags.Append( "i" );
			if( Options.m ) flags.Append( "m" );
			if( Options.s ) flags.Append( "s" );
			if( Options.x ) flags.Append( "x" );

			var obj = new
			{
				p = Pattern,
				t = Text,
				f = flags.ToString( ),
			};

			string json = JsonSerializer.Serialize( obj );
			string stdout_contents;
			string stderr_contents;

			if( !InvokeDClient( cnc, json, out stdout_contents, out stderr_contents ) )
			{
				return RegexMatches.Empty;
			}

			if( !string.IsNullOrWhiteSpace( stderr_contents ) )
			{
				throw new Exception( stderr_contents );
			}

			var response = JsonSerializer.Deserialize<DClientMatchesResponse>( stdout_contents );

			var matches = new List<IMatch>( );

			foreach( var m in response.matches )
			{
				SimpleMatch match = null;

				for( int group_index = 0; group_index < m.groups.Length; group_index++ )
				{
					int[] g = m.groups[group_index];
					bool success = g.Length == 2;

					if( group_index == 0 && !success )
					{
						// if pattern is "()", which matches any position, 'std.regex' does not return captures, 
						// even the main one (all are null); however the match object contains the valid index;
						// this is a workaround:

						success = true;
						g = new[] { m.index, 0 };
					}

					int byte_start = success ? g[0] : 0;
					int byte_end = byte_start + ( success ? g[1] : 0 );
					int byte_length = byte_end - byte_start;

					int char_start = Encoding.UTF8.GetCharCount( text_utf8_bytes, 0, byte_start );
					int char_end = Encoding.UTF8.GetCharCount( text_utf8_bytes, 0, byte_end );
					int char_length = char_end - char_start;

					if( group_index == 0 )
					{
						Debug.Assert( match == null );
						Debug.Assert( success );

						match = SimpleMatch.Create( char_start, char_end - char_start, this );
					}

					Debug.Assert( match != null );

					// try to identify the named group by index and length;
					// cannot be done univocally in situations like "(?P<name1>(?P<name2>(.))", when index and length are the same

					string name;

					var np = m.named_groups
						.Where( _ => group_index != 0 )
						.Select( ( ng, j ) => new { ng, j } )
						.Where( p => p.ng[0] >= 0 )
						.FirstOrDefault( z => z.ng[0] == byte_start && z.ng[1] == byte_length && !match.Groups.Any( q => q.Name == response.names[z.j] ) );

					if( np == null )
					{
						name = null;
					}
					else
					{
						name = response.names[np.j];
					}

					if( string.IsNullOrWhiteSpace( name ) ) name = group_index.ToString( CultureInfo.InvariantCulture );

					if( success )
					{
						match.AddGroup( char_start, char_length, true, name );
					}
					else
					{
						match.AddGroup( 0, 0, false, name );
					}
				}

				Debug.Assert( match != null );

				matches.Add( match );
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


		static string GetDClientExePath( )
		{
			string assembly_location = Assembly.GetExecutingAssembly( ).Location;
			string assembly_dir = Path.GetDirectoryName( assembly_location );
			string d_client_exe = Path.Combine( assembly_dir, @"DClient.bin" );

			return d_client_exe;
		}


		static bool InvokeDClient( ICancellable cnc, string stdinContents, out string stdoutContents, out string stderrContents )
		{
			return ProcessUtilities.InvokeExe( cnc, GetDClientExePath( ), null, stdinContents, out stdoutContents, out stderrContents, EncodingEnum.UTF8 );
		}
	}


	class DClientVersionResponse
	{
		public string version { get; set; }
	}


	public class DClientMatchesResponse
	{
		public string[] names { get; set; }
		public DClientOneMatch[] matches { get; set; }
	}


	public class DClientOneMatch
	{
		[JsonPropertyName( "i" )]
		public int index { get; set; } // byte-index of the whole match
		[JsonPropertyName( "g" )]
		public int[][] groups { get; set; } // [byte-index, byte-length], or [-1, 0] if failed
		[JsonPropertyName( "n" )]
		public int[][] named_groups { get; set; } // [byte-index, byte-length], or [-1, 0] if failed
	}

}
