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


namespace PerlRegexEngineNs
{
	public class PerlRegexEngine : IRegexEngine
	{
		readonly UCPerlRegexOptions OptionsControl;

		static readonly Dictionary<string, Regex> CachedColouringRegexes = new Dictionary<string, Regex>( );
		static readonly Dictionary<string, Regex> CachedHighlightingRegexes = new Dictionary<string, Regex>( );


		[DllImport( "kernel32", CharSet = CharSet.Unicode, SetLastError = true )]
		static extern bool SetDllDirectory( string lpPathName );


		static PerlRegexEngine( )
		{
			Assembly current_assembly = Assembly.GetExecutingAssembly( );
			string current_assembly_path = Path.GetDirectoryName( current_assembly.Location );
			string dll_path = Path.Combine( current_assembly_path, @"Perl-min\perl\bin" );

			bool b = SetDllDirectory( dll_path );
			if( !b ) throw new ApplicationException( $"SetDllDirectory failed: '{dll_path}'" );
		}

		public PerlRegexEngine( )
		{
			OptionsControl = new UCPerlRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}

		#region IRegexEngine

		public string Id => "Perl";

		public string Name => "Perl";

		public string EngineVersion => GetPerlVersion( );

		public RegexEngineCapabilityEnum Capabilities => RegexEngineCapabilityEnum.NoCaptures | RegexEngineCapabilityEnum.CombineSurrogatePairs;

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


		string GetPerlVersion( )
		{
			if( PerlVersion == null )
			{
				lock( Locker )
				{
					if( PerlVersion == null )
					{
						string assembly_location = Assembly.GetExecutingAssembly( ).Location;
						string assembly_dir = Path.GetDirectoryName( assembly_location );
						string perl_dir = Path.Combine( assembly_dir, @"Perl-min\perl" );
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
							if( PerlVersion.StartsWith( "v" ) ) PerlVersion = PerlVersion.Substring( 1 );
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
			var pb_escape = new PatternBuilder( );

			pb_escape.BeginGroup( "escape" );

			pb_escape.Add( @"\\c[A-Za-z]" ); // control char
			pb_escape.Add( @"\\x[0-9a-fA-F]{1,2}" ); // hexa, two digits
			pb_escape.Add( @"\\x\{[0-9a-fA-F]*(\} | $)" ); // hexa, error if empty
			pb_escape.Add( @"\\N\{.*?(\} | $)" ); // Unicode name or hexa
			pb_escape.Add( @"\\0[0-7]{1,2}" ); // octal, two digits after 0
			pb_escape.Add( @"\\[0-7]{1,3}" ); // octal, three digits
			pb_escape.Add( @"\\o\{[0-9]+(\} | $)" ); // octal
			pb_escape.Add( @"\\[pP]([a-zA-Z] | $)" ); // property
			pb_escape.Add( @"\\[pP]\{.*?(\} | $)" ); // property
			pb_escape.Add( @"\\Q.*?(\\E|$)" ); // quoted sequence, \Q...\E
			pb_escape.Add( @"\\[bB]\{.*?(\} | $)" ); // Unicode boundary
			pb_escape.Add( @"\\." );

			pb_escape.EndGroup( );

			// 

			var pb_class = new PatternBuilder( ).AddGroup( "class", @"\[(?'c'[:]) .*? (\k<c>\] | $)" );  // [: ... :]

			//

			var pb = new PatternBuilder( );

			pb.BeginGroup( "comment" );

			pb.Add( @"\(\?\#.*?(\)|$)" ); // comment
			if( isXorXx ) pb.Add( @"\#.*?(\n|$)" ); // line-comment*/

			pb.EndGroup( );

			//

			pb.Add( @"\(\?(?'name'<(?![=!]).*?(>|$))" );
			pb.Add( @"\(\?(?'name''.*?('|$))" );

			pb.Add( @"\(\?P(?'name'<.*?(>|$))" );
			pb.Add( @"\(\?P(?'name'[=>].*?(\)|$))" );

			pb.Add( @"(?'name'\\g[0-9]+)" );
			pb.Add( @"(?'name'\\[gk]\{.*?(\}|$))" );
			pb.Add( @"(?'name'\\[gk]<.*?(>|$))" );
			pb.Add( @"(?'name'\\k'.*?('|$))" );

			//

			pb.Add( pb_escape.ToPattern( ) );

			//

			string char_group = $@"( \[ \]? ({pb_class.ToPattern( )} | {pb_escape.ToPattern( )} | . )*? (\]|$) )";

			pb.Add( char_group );

			// 

			return pb.ToRegex( );
		}


		Regex CreateHighlightingRegex( bool isXorXX )
		{
			var pb = new PatternBuilder( );

			pb.Add( @"(\(\?\#.*?(\)|$))" ); // comment
			if( isXorXX ) pb.Add( @"(\#[^\n]*)" ); // line comment
			pb.Add( @"\\Q.*?(\\E|$)" ); // quoted sequence, \Q...\E
			pb.Add( @"\\[xNopPbBgk]\{.*?(\}|$)" ); // (skip)
			pb.Add( @"(?'left_par'\()" ); // '('
			pb.Add( @"(?'right_par'\))" ); // ')'
			pb.Add( @"(?'left_brace'\{) \s* \d+ \s* (,\s*\d*)? \s* ((?'right_brace'\})|$)" ); // '{...}'
			pb.Add( @"((?'left_bracket'\[) \]? ((\[:.*? (:\]|$)) | \\. | .)*? ((?'right_bracket'\])|$) )" ); // [...]
			pb.Add( @"\\." ); // '\...'

			return pb.ToRegex( );
		}

	}
}
