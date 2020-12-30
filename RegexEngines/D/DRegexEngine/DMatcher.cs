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

					int byte_start = success ? g[0] : 0;
					int byte_end = byte_start + ( success ? g[1] : 0 );

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

					string name = null;//.......... response.names[group_index];
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
			return ProcessUtilities.InvokeExe( cnc, GetDClientExePath( ), null, stdinContents, out stdoutContents, out stderrContents );
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
		[JsonPropertyName( "g" )]
		public int[][] groups { get; set; }
		[JsonPropertyName( "n" )]
		public int[] named_positions { get; set; }
	}

}
