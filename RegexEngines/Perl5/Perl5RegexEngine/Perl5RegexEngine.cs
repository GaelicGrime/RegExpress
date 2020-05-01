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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;


namespace Perl5RegexEngineNs
{
	public class Perl5RegexEngine : IRegexEngine
	{
		readonly UCPerl5RegexOptions OptionsControl;

		static readonly Dictionary<string, Regex> CachedColouringRegexes = new Dictionary<string, Regex>( );
		static readonly Dictionary<string, Regex> CachedHighlightingRegexes = new Dictionary<string, Regex>( );


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
			Regex regex = GetCachedColouringRegex( );

			foreach( Match m in regex.Matches( pattern ) )
			{
				Debug.Assert( m.Success );

				if( cnc.IsCancellationRequested ) return;

				// escapes, '\...'
				{
					var g = m.Groups["escape"];
					if( g.Success )
					{
						if( cnc.IsCancellationRequested ) return;

						foreach( Capture c in g.Captures )
						{
							if( cnc.IsCancellationRequested ) return;

							var intersection = Segment.Intersection( visibleSegment, c.Index, c.Length );

							if( !intersection.IsEmpty )
							{
								colouredSegments.Escapes.Add( intersection );
							}
						}

						continue;
					}
				}

				if( cnc.IsCancellationRequested ) return;

				// comments, '(?#...)', '#...'
				{
					var g = m.Groups["comment"];
					if( g.Success )
					{
						if( cnc.IsCancellationRequested ) return;

						foreach( Capture c in g.Captures )
						{
							if( cnc.IsCancellationRequested ) return;

							var intersection = Segment.Intersection( visibleSegment, c.Index, c.Length );

							if( !intersection.IsEmpty )
							{
								colouredSegments.Comments.Add( intersection );
							}
						}

						continue;
					}
				}

				if( cnc.IsCancellationRequested ) return;

				// class (within [...] groups), '[:...:]'
				{
					var g = m.Groups["class"];
					if( g.Success )
					{
						if( cnc.IsCancellationRequested ) return;

						foreach( Capture c in g.Captures )
						{
							if( cnc.IsCancellationRequested ) return;

							var intersection = Segment.Intersection( visibleSegment, c.Index, c.Length );

							if( !intersection.IsEmpty )
							{
								colouredSegments.Escapes.Add( intersection );
							}
						}

						continue;
					}
				}

				if( cnc.IsCancellationRequested ) return;

				// named group, '(?<name>...)' or '(?'name'...)' and others
				{
					var g = m.Groups["name"];
					if( g.Success )
					{
						if( cnc.IsCancellationRequested ) return;

						foreach( Capture c in g.Captures )
						{
							if( cnc.IsCancellationRequested ) return;

							var intersection = Segment.Intersection( visibleSegment, c.Index, c.Length );

							if( !intersection.IsEmpty )
							{
								colouredSegments.GroupNames.Add( intersection );
							}
						}

						continue;
					}
				}
			}
		}


		public void HighlightPattern( ICancellable cnc, Highlights highlights, string pattern, int selectionStart, int selectionEnd, Segment visibleSegment )
		{
			int par_size = 1;
			int bracket_size = 1;

			Regex regex = GetCachedHighlightingRegex( );

			HighlightHelper.CommonHighlighting( cnc, highlights, pattern, selectionStart, selectionEnd, visibleSegment, regex, par_size, bracket_size );
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


		Regex GetCachedColouringRegex( )
		{
			bool is_x = OptionsControl.IsModifierSelected( "x" );
			bool is_xx = OptionsControl.IsModifierSelected( "xx" );
			bool is_x_or_xx = is_x || is_xx;

			string key = string.Join( "\u001F", new object[] { is_x_or_xx } );

			lock( CachedColouringRegexes )
			{
				if( CachedColouringRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateColouringRegex( is_x_or_xx );

				CachedColouringRegexes.Add( key, regex );

				return regex;
			}
		}

		Regex GetCachedHighlightingRegex( )
		{
			bool is_x = OptionsControl.IsModifierSelected( "x" );
			bool is_xx = OptionsControl.IsModifierSelected( "xx" );
			bool is_x_or_xx = is_x || is_xx;

			string key = string.Join( "\u001F", new object[] { is_x_or_xx } );

			lock( CachedHighlightingRegexes )
			{
				if( CachedHighlightingRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateHighlightingRegex( is_x_or_xx );

				CachedHighlightingRegexes.Add( key, regex );

				return regex;
			}
		}


		Regex CreateColouringRegex( bool isXorXx )
		{
			string escape = "";

			escape += @"\\c[A-Za-z] | "; // control char
			escape += @"\\x[0-9a-fA-F]{1,2} | "; // hexa, two digits
			escape += @"\\x\{[0-9a-fA-F]*(\} | $) | "; // hexa, error if empty
			escape += @"\\N\{(U\+)?[0-9a-fA-F]+(\} | $) | "; // Unicode name or hexa
			escape += @"\\0[0-7]{1,2} | "; // octal, two digits after 0
			escape += @"\\[0-7]{1,3} | "; // octal, three digits
			escape += @"\\o\{[0-9]+(\} | $) | "; // octal
			escape += @"\\[pP]([a-zA-Z] | $) | "; // property
			escape += @"\\[pP]\{.*?(\} | $) | "; // property
			escape += @"\\Q.*?(\\E|$) | "; // quoted sequence, \Q...\E
			escape += @"\\[bB]\{.*?(\} | $) | "; // Unicode boundary

			escape += @"\\. | ";

			escape = RegexUtilities.EndGroup( escape, "escape" );

			// 

			string @class = "";

			@class += @"\[(?'c'[:]) .*? (\k<c>\] | $) | ";  // [: ... :]

			@class = RegexUtilities.EndGroup( @class, "class" );

			//

			string char_group = "";

			char_group += @"\[ \]? (" + @class + " | " + escape + " | . " + @")*? (\]|$) | ";

			char_group = RegexUtilities.EndGroup( char_group, null );

			// 

			string comment = "";

			comment += @"\(\?\#.*?(\)|$) | "; // comment
			if( isXorXx ) comment += @"\#.*?(\n|$) | "; // line-comment*/

			comment = RegexUtilities.EndGroup( comment, "comment" );

			//

			string named_group = "";

			named_group += @"\(\?(?'name'<(?![=!]).*?(>|$)) | ";
			named_group += @"\(\?(?'name''.*?('|$)) | ";


			named_group += @"\(\?P(?'name'<.*?(>|$)) | ";
			named_group += @"\(\?P(?'name'[=>].*?(\)|$)) | ";

			named_group += @"(?'name'\\g[0-9]+) | ";
			named_group += @"(?'name'\\[gk]\{.*?(\}|$)) | ";
			named_group += @"(?'name'\\[gk]<.*?(>|$)) | ";
			named_group += @"(?'name'\\k'.*?('|$)) | ";

			named_group = RegexUtilities.EndGroup( named_group, "named_group" );

			//

			string[] all = new[]
			{
				comment,
				named_group,
				escape,
				char_group,
			};

			string pattern = @"(?nsx)(" + Environment.NewLine +
				string.Join( " | " + Environment.NewLine, all.Where( s => !string.IsNullOrWhiteSpace( s ) ) ) +
				")";

			var regex = new Regex( pattern, RegexOptions.Compiled | RegexOptions.ExplicitCapture );

			return regex;
		}


		Regex CreateHighlightingRegex( bool isXorXX )
		{
			string pattern = "(?nsx)(";
			pattern += @"(\(\?\#.*?(\)|$)) | "; // comment
			if( isXorXX ) pattern += @"(\#[^\n]*) | "; // line comment
			pattern += @"(?'left_par'\() | "; // '('
			pattern += @"(?'right_par'\)) | "; // ')'
			pattern += @"(?'left_brace'\{).*?((?'right_brace'\})|$) | "; // '{...}'
			pattern += @"((?'left_bracket'\[) \]? ((\[:.*? (:\]|$)) | \\. | .)*? ((?'right_bracket'\])|$) ) | "; // [...]
			pattern += @"\\."; // '\...'
			pattern += @")";

			var regex = new Regex( pattern, RegexOptions.Compiled | RegexOptions.ExplicitCapture );

			return regex;
		}

	}
}
