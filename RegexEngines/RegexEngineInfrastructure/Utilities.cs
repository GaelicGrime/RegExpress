using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure
{
	public static class CheckedCast
	{
		public static Int32 ToInt32( UInt64 v )
		{
			return checked((Int32)v);
		}


		public static Int32 ToInt32n( UInt64 v )
		{
			if( v == UInt64.MaxValue ) return -1;

			return checked((Int32)v);
		}


		public static Int32 ToInt32( Int64 v )
		{
			return checked((Int32)v);
		}


		public static Int32 ToInt32( UInt32 v )
		{
			return checked((Int32)v);
		}


		[Obsolete( "This should not be achieved.", error: true )]
		public static void ToInt32<T>( T v )
		{
		}

	}


	public static class ProcessUtilities
	{

		static readonly UTF8Encoding Utf8Encoding = new UTF8Encoding( encoderShouldEmitUTF8Identifier: false );


		public static bool InvokeExe( ICancellable cnc, string exePath, string arguments, string stdinContents, out string stdoutContents, out string stderrContents )
		{
			var output_sb = new StringBuilder( );
			var error_sb = new StringBuilder( );

			using( Process p = new Process( ) )
			{
				p.StartInfo.FileName = exePath;
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
