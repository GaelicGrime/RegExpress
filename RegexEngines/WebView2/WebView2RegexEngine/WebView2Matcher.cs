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
using System.Threading.Tasks;

namespace WebView2RegexEngineNs
{
	public class WebView2Matcher : IMatcher
	{

		public class VersionResponse
		{
			public string v { get; set; }
		}


		public class MatchResponse // (a list of such objects)
		{
			public Dictionary<string, int[]> g { get; set; }
			public List<int[]> i { get; set; }
		}


		readonly WebView2RegexOptions Options;
		readonly string Pattern;


		public WebView2Matcher( string pattern, WebView2RegexOptions options )
		{
			Pattern = pattern;
			Options = options;
		}


		internal static string GetVersion( ICancellable cnc )
		{
			string stdout_contents;
			string stderr_contents;

			string version;

			if( !ProcessUtilities.InvokeExe( cnc, GetClientExePath( ), "v", "", out stdout_contents, out stderr_contents, EncodingEnum.UTF8 ) ||
				!string.IsNullOrWhiteSpace( stderr_contents ) ||
				string.IsNullOrWhiteSpace( stdout_contents ) )
			{
				version = "Unknown version";
			}
			else
			{
				try
				{
					var v = JsonSerializer.Deserialize<VersionResponse>( stdout_contents );

					version = v.v;
				}
				catch( Exception )
				{
					if( Debugger.IsAttached ) Debugger.Break( );

					version = "Unknown version";
				}
			}

			return version;
		}

		#region IMatcher


		public RegexMatches Matches( string text, ICancellable cnc )
		{

			string flags = string.Concat(
				Options.i ? "i" : "",
				Options.m ? "m" : "",
				Options.s ? "s" : "",
				Options.u ? "u" : ""
				);

			string stdout_contents;
			string stderr_contents;

			Action<StreamWriter> stdin_writer = new Action<StreamWriter>( sw =>
			{
				sw.Write( "m \"" );
				WriteJavaScriptString( sw, Pattern );
				sw.Write( "\" \"" );
				sw.Write( flags );
				sw.Write( "\" \"" );
				WriteJavaScriptString( sw, text );
				sw.Write( "\"" );
			} );

			if( !ProcessUtilities.InvokeExe( cnc, GetClientExePath( ), "i", stdin_writer, out stdout_contents, out stderr_contents, EncodingEnum.UTF8 ) )
			{
				return RegexMatches.Empty; // (cancelled)
			}

			if( !string.IsNullOrWhiteSpace( stderr_contents ) )
			{
				throw new Exception( stderr_contents );
			}

			SimpleTextGetter stg = new SimpleTextGetter( text );

			List<MatchResponse> client_matches = JsonSerializer.Deserialize<List<MatchResponse>>( stdout_contents );

			if( client_matches == null )
			{
				throw new Exception( "Failed" );
			}

			List<IMatch> matches = new List<IMatch>( );

			foreach( var cm in client_matches )
			{
				if( cm.i.Any( ) )
				{
					var start = cm.i[0][0];
					var end = cm.i[0][1];

					var sm = SimpleMatch.Create( start, end - start, stg );

					// TODO: determine names

					sm.AddGroup( sm.Index, sm.Length, true, "0" ); // (default group)

					for( int j = 1; j < cm.i.Count; ++j )
					{
						var name = j.ToString( CultureInfo.InvariantCulture );
						var g = cm.i[j];

						if( g == null )
						{
							sm.AddGroup( -1, 0, false, name );
						}
						else
						{
							start = cm.i[j][0];
							end = cm.i[j][1];

							sm.AddGroup( start, end - start, true, name );
						}
					}

					matches.Add( sm );
				}
			}

			return new RegexMatches( matches.Count, matches );
		}

		#endregion IMatcher


		private static void WriteJavaScriptString( TextWriter sw, string text )
		{
			foreach( var c in text )
			{
				if( ( c >= 'a' && c <= 'z' ) || ( c >= 'A' && c <= 'Z' ) || ( c >= '0' && c <= '9' ) ||
					"`~!@#$%*()-_=+[]{};:',./?".Contains( c ) )
				{
					sw.Write( c );
				}
				else
				{
					sw.Write( "\\u{0:X4}", unchecked((uint)c) );
				}
			}
		}


		static string GetClientExePath( )
		{
			string assembly_location = Assembly.GetExecutingAssembly( ).Location;
			string assembly_dir = Path.GetDirectoryName( assembly_location );
			string client_exe = Path.Combine( assembly_dir, @"WebView2Client.bin" );

			return client_exe;
		}
	}
}
