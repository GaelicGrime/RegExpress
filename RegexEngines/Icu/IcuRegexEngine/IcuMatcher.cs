using RegexEngineInfrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace IcuRegexEngineNs
{
	class IcuMatcher
	{


		public static string GetIcuVersion( ICancellable cnc )
		{
			string stdout_contents;
			string stderr_contents;

			if( !InvokeIcuClient( cnc, "v", out stdout_contents, out stderr_contents ) ) return null;

			if( !string.IsNullOrWhiteSpace( stderr_contents ) )
			{
				throw new Exception( stderr_contents );
			}

			var version = stdout_contents.Trim();

			return version;
		}







		static string GetDClientExePath( )
		{
			string assembly_location = Assembly.GetExecutingAssembly( ).Location;
			string assembly_dir = Path.GetDirectoryName( assembly_location );
			string d_client_exe = Path.Combine( assembly_dir, @"IcuClient.bin" );

			return d_client_exe;
		}


		static bool InvokeIcuClient( ICancellable cnc, string stdinContents, out string stdoutContents, out string stderrContents )
		{
			return ProcessUtilities.InvokeExe( cnc, GetDClientExePath( ), null, stdinContents, out stdoutContents, out stderrContents, unicode: true );
		}


		public void Test( )
		{
			string stdout_contents;
			string stderr_contents;

			string longstr = new String( 'x', 170000 ) + "end";

			ProcessUtilities.InvokeExe( NonCancellable.Instance, GetDClientExePath( ), "", longstr, out stdout_contents, out stderr_contents, unicode: true );

		}


	}
}
