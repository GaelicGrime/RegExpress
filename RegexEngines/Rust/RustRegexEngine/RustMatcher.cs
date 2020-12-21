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
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace RustRegexEngineNs
{
	sealed class RustMatcher : IMatcher, ISimpleTextGetter
	{
		static readonly UTF8Encoding Utf8Encoding = new UTF8Encoding( encoderShouldEmitUTF8Identifier: false );

		readonly RustRegexOptions Options;
		readonly string Pattern;
		string Text;
		byte[] TextUtf8Bytes;

		public RustMatcher( string pattern, RustRegexOptions options )
		{
			Options = options;
			Pattern = pattern;
		}


		public static string GetRustVersion( ICancellable cnc )
		{
			string stdout_contents;
			string stderr_contents;

			if( !InvokeRustClient( cnc, "{\"c\":\"v\"}", out stdout_contents, out stderr_contents ) ) return null;

			if( !string.IsNullOrWhiteSpace( stderr_contents ) )
			{
				throw new Exception( stderr_contents );
			}

			var v = JsonSerializer.Deserialize<RustClientVersionResponse>( stdout_contents );

			return v.version;
		}


		#region IMatcher

		public RegexMatches Matches( string text, ICancellable cnc )
		{
			Text = text;
			TextUtf8Bytes = Utf8Encoding.GetBytes( text );

			string stdout_contents;
			string stderr_contents;

			StringBuilder o = new StringBuilder( );

			if( Options.case_insensitive ) o.Append( "i" );
			if( Options.multi_line ) o.Append( "m" );
			if( Options.dot_matches_new_line ) o.Append( "s" );
			if( Options.swap_greed ) o.Append( "U" );
			if( Options.ignore_whitespace ) o.Append( "x" );
			if( Options.unicode ) o.Append( "u" );
			if( Options.octal ) o.Append( "O" );

			var obj = new
			{
				s = Options.@struct,
				p = Pattern,
				t = Text,
				o = o.ToString( ),
				sl = Options.size_limit?.Trim( ) ?? "",
				dsl = Options.dfa_size_limit?.Trim( ) ?? "",
				nl = Options.nest_limit?.Trim( ) ?? "",
			};

			string json = JsonSerializer.Serialize( obj );

			if( !InvokeRustClient( cnc, json, out stdout_contents, out stderr_contents ) )
			{
				return new RegexMatches( 0, Enumerable.Empty<IMatch>( ) );
			}

			if( !string.IsNullOrWhiteSpace( stderr_contents ) )
			{
				throw new Exception( stderr_contents );
			}

			var rcr = JsonSerializer.Deserialize<RustClientMatchesResponse>( stdout_contents );

			var matches = new List<IMatch>( );

			foreach( var m in rcr.matches )
			{
				SimpleMatch match = null;

				for( int group_index = 0; group_index < m.Length; group_index++ )
				{
					int[] g = m[group_index];
					bool success = g.Length == 2;

					int byte_start = success ? g[0] : 0;
					int byte_end = success ? g[1] : 0;

					int char_start = Utf8Encoding.GetCharCount( TextUtf8Bytes, 0, byte_start );
					int char_end = Utf8Encoding.GetCharCount( TextUtf8Bytes, 0, byte_end );
					int char_length = char_end - char_start;

					if( group_index == 0 )
					{
						Debug.Assert( match == null );
						Debug.Assert( success );

						match = SimpleMatch.Create( char_start, char_end - char_start, this );
					}

					Debug.Assert( match != null );

					string name = rcr.names[group_index];
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


		static string GetRustClientExePath( )
		{
			string assembly_location = Assembly.GetExecutingAssembly( ).Location;
			string assembly_dir = Path.GetDirectoryName( assembly_location );
			string rust_client_exe = Path.Combine( assembly_dir, @"RustClient.bin" );

			return rust_client_exe;
		}


		static bool InvokeRustClient( ICancellable cnc, string stdinContents, out string stdoutContents, out string stderrContents )
		{
			var output_sb = new StringBuilder( );
			var error_sb = new StringBuilder( );

			using( Process p = new Process( ) )
			{
				p.StartInfo.FileName = GetRustClientExePath( );
				//p.StartInfo.Arguments = arguments;

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

				using( StreamWriter sw = new StreamWriter( p.StandardInput.BaseStream, Utf8Encoding ) )
				{
					sw.WriteLine( stdinContents );
				}

				// TODO: use timeout

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
					catch( Exception )
					{
						if( Debugger.IsAttached ) Debugger.Break( );

						// ignore
					}

					stdoutContents = null;
					stderrContents = null;

					return false;
				}

				Debug.Assert( done );
			}

			stderrContents = error_sb.ToString( );
			stdoutContents = output_sb.ToString( );

			return true;
		}

	}
}


class RustClientVersionResponse
{
	public string version { get; set; }
}


class RustClientMatchesResponse
{
	public string[] names { get; set; }
	public int[][][] matches { get; set; }
}
