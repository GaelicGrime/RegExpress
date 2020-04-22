using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.SyntaxColouring;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Perl5RegexEngineNs
{
	public class Perl5RegexEngine : IRegexEngine
	{
		readonly UCPerl5RegexOptions OptionsControl;


		[DllImport( "kernel32", CharSet = CharSet.Unicode, SetLastError = true )]
		static extern bool SetDllDirectory( string lpPathName );


		static Perl5RegexEngine( )
		{
			Assembly current_assembly = Assembly.GetExecutingAssembly( );
			string current_assembly_path = Path.GetDirectoryName( current_assembly.Location );
			string dll_path = Path.Combine( current_assembly_path, @"Perl5-min\perl\bin" );

			bool b = SetDllDirectory( dll_path );
			if( !b ) throw new ApplicationException( $"SetDllDirectory failed: '{dll_path}'" );
		}

		public Perl5RegexEngine( )
		{
			OptionsControl = new UCPerl5RegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}

		#region IRegexEngine

		public string Id => "Perl5";

		public string Name => "Perl5";

		public string EngineVersion => GetPerl5Version( );

		public RegexEngineCapabilityEnum Capabilities => RegexEngineCapabilityEnum.NoCaptures;

		public string NoteForCaptures => null;

		public event RegexEngineOptionsChanged OptionsChanged;

		public Control GetOptionsControl( )
		{
			return OptionsControl;
		}

		public string[] ExportOptions( )
		{
			return OptionsControl.ExportOptions( );
		}


		public void ImportOptions( string[] options )
		{
			OptionsControl.ImportOptions( options );
		}

		public IMatcher ParsePattern( string pattern )
		{
			string[] selected_options = OptionsControl.CachedOptions;

			return new Matcher( pattern, selected_options );
		}

		public void ColourisePattern( ICancellable cnc, ColouredSegments colouredSegments, string pattern, Segment visibleSegment )
		{
		}

		public void HighlightPattern( ICancellable cnc, Highlights highlights, string pattern, int selectionStart, int selectionEnd, Segment visibleSegment )
		{

		}

		#endregion IRegexEngine

		private void OptionsControl_Changed( object sender, RegexEngineOptionsChangedArgs args )
		{
			OptionsChanged?.Invoke( this, args );
		}


		static string PerlVersion = null;
		static readonly object Locker = new object( );


		string GetPerl5Version( )
		{
			if( PerlVersion == null )
			{
				lock( Locker )
				{
					if( PerlVersion == null )
					{
						string assembly_location = Assembly.GetExecutingAssembly( ).Location;
						string assembly_dir = Path.GetDirectoryName( assembly_location );
						string perl_dir = Path.Combine( assembly_dir, @"Perl5-min\perl" );
						string perl_exe = Path.Combine( perl_dir, @"bin\perl.exe" );

						var psi = new ProcessStartInfo( );

						psi.FileName = perl_exe;
						psi.Arguments = @"-CS -e ""print 'V=', $^V""";

						psi.UseShellExecute = false;
						psi.RedirectStandardInput = true;
						psi.RedirectStandardOutput = true;
						psi.StandardOutputEncoding = Encoding.UTF8;
						psi.CreateNoWindow = true;
						psi.WindowStyle = ProcessWindowStyle.Hidden;

						string output;

						using( Process p = Process.Start( psi ) )
						{
							output = p.StandardOutput.ReadToEnd( );
						}

						if( !output.StartsWith( "V=" ) )
						{
							if( Debugger.IsAttached ) Debugger.Break( );
							Debug.WriteLine( "Unknown Perl Get-Version: '{0}'", output );
							PerlVersion = "unknown version";
						}
						else
						{
							PerlVersion = output.Substring( "V=".Length );
						}
					}
				}
			}

			return PerlVersion;
		}
	}
}
