using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.Matches.Simple;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace RustRegexEngineNs.Matches
{
	class RustMatcher : IMatcher, ISimpleTextGetter
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
					sw.WriteLine( "v" );
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

					return null;
				}

				Debug.Assert( done );
			}

			string error = error_sb.ToString( );

			if( !string.IsNullOrWhiteSpace( error ) )
			{
				throw new Exception( error );
			}

			string output = output_sb.ToString( );

			return output.Trim( );
		}


		#region IMatcher

		public RegexMatches Matches( string text, ICancellable cnc )
		{
			Text = text;
			TextUtf8Bytes = Utf8Encoding.GetBytes( text );

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
					sw.Write( "&p=" );
					sw.Write( Uri.EscapeDataString( Pattern ) );
					sw.Write( "&t=" );
					sw.Write( Uri.EscapeDataString( text ) );

					sw.Write( "&s=" );
					sw.Write( Uri.EscapeDataString( Options.@struct ) );

					StringBuilder options = new StringBuilder( );

					if( Options.case_insensitive ) options.Append( "i" );
					if( Options.multi_line ) options.Append( "m" );
					if( Options.dot_matches_new_line ) options.Append( "s" );
					if( Options.swap_greed ) options.Append( "S" );
					if( Options.ignore_whitespace ) options.Append( "x" );
					if( Options.unicode ) options.Append( "U" );
					if( Options.octal ) options.Append( "O" );

					sw.Write( "&o=" );
					sw.Write( Uri.EscapeDataString( options.ToString( ) ) );

					sw.WriteLine( );
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

					return new RegexMatches( 0, Enumerable.Empty<IMatch>( ) );
				}

				Debug.Assert( done );
			}

			string error = error_sb.ToString( );

			if( !string.IsNullOrWhiteSpace( error ) )
			{
				throw new Exception( error );
			}

			string output = output_sb.ToString( );

			var names = new List<string>( );
			var matches = new List<IMatch>( );

			using( var sr = new StringReader( output ) )
			{
				string line;
				SimpleMatch match = null;
				int group_index = 0;

				while( ( line = sr.ReadLine( ) ) != null )
				{
					if( line.StartsWith( "D:" ) )
					{
						// (for debugging)
						continue;
					}

					if( line.StartsWith( "N: " ) )
					{
						names.Add( line.Substring( 2 ).Trim( ) );
						continue;
					}

					if( line == "--M--" )
					{
						if( match != null )
						{
							matches.Add( match );
							match = null;
							group_index = 0;
						}
						continue;
					}

					var m = Regex.Match( line, @"^\s*G:\s*(\d+)\s+(\d+)\s*$" );
					if( m.Success )
					{
						int byte_start = int.Parse( m.Groups[1].Value );
						int byte_end = int.Parse( m.Groups[2].Value );

						int char_start = Utf8Encoding.GetCharCount( TextUtf8Bytes, 0, byte_start );
						int char_end = Utf8Encoding.GetCharCount( TextUtf8Bytes, 0, byte_end );

						if( match == null )
						{
							Debug.Assert( group_index == 0 );
							match = SimpleMatch.Create( char_start, char_end - char_start, this );
						}

						match.AddGroup( char_start, char_end - char_start, true, names[group_index] );
						++group_index;

						continue;
					}

					m = Regex.Match( line, @"^\s*G:\s*$" );
					if( m.Success )
					{
						match.AddGroup( 0, 0, false, names[group_index] );
						++group_index;

						continue;
					}

					if( string.IsNullOrWhiteSpace( line ) ) continue;

					throw new Exception( "Unrecognised response:\r\n" + output );
				}

				if( match != null )
				{
					matches.Add( match );
					match = null;
					group_index = 0;
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



		static string GetRustClientExePath( )
		{
			string assembly_location = Assembly.GetExecutingAssembly( ).Location;
			string assembly_dir = Path.GetDirectoryName( assembly_location );
			string rust_client_exe = Path.Combine( assembly_dir, @"RustClient.bin" );

			return rust_client_exe;
		}




	}
}
