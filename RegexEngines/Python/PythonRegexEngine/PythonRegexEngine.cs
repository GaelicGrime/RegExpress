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
using System.Text.RegularExpressions;
using System.Windows.Controls;


namespace PythonRegexEngineNs
{
	public class PythonRegexEngine : IRegexEngine
	{
		readonly UCPythonRegexOptions OptionsControl;
		static readonly Lazy<string> LazyVersion = new Lazy<string>( GetVersion );

		static readonly Dictionary<string, Regex> CachedColouringRegexes = new Dictionary<string, Regex>( );
		static readonly Dictionary<string, Regex> CachedHighlightingRegexes = new Dictionary<string, Regex>( );


		[DllImport( "kernel32", CharSet = CharSet.Unicode, SetLastError = true )]
		static extern bool SetDllDirectory( string lpPathName );


		static PythonRegexEngine( )
		{
			Assembly current_assembly = Assembly.GetExecutingAssembly( );
			string current_assembly_path = Path.GetDirectoryName( current_assembly.Location );
			string dll_path = Path.Combine( current_assembly_path, @"Python-embed" );

			bool b = SetDllDirectory( dll_path );
			if( !b ) throw new ApplicationException( $"SetDllDirectory failed: '{dll_path}'" );
		}

		public PythonRegexEngine( )
		{
			OptionsControl = new UCPythonRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "Python";

		public string Name => "Python";

		public string EngineVersion => LazyVersion.Value;

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

				// named groups, '(?P<name>...)' 
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


		Regex GetCachedColouringRegex( )
		{
			bool is_verbose = OptionsControl.IsFlagSelected( "VERBOSE" );

			string key = string.Join( "\u001F", new object[] { is_verbose } );

			lock( CachedColouringRegexes )
			{
				if( CachedColouringRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateColouringRegex( is_verbose );

				CachedColouringRegexes.Add( key, regex );

				return regex;
			}
		}


		Regex GetCachedHighlightingRegex( )
		{
			bool is_verbose = OptionsControl.IsFlagSelected( "VERBOSE" );

			string key = string.Join( "\u001F", new object[] { is_verbose } );

			lock( CachedHighlightingRegexes )
			{
				if( CachedHighlightingRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateHighlightingRegex( is_verbose );

				CachedHighlightingRegexes.Add( key, regex );

				return regex;
			}
		}


		Regex CreateColouringRegex( bool isVerbose )
		{
			var pb_escape = new PatternBuilder( );

			pb_escape.BeginGroup( "escape" );

			pb_escape.Add( @"\\x[0-9a-fA-F]{1,2}" ); // hexa, two digits
			pb_escape.Add( @"\\0[0-7]+" ); // octal, after '0'
			pb_escape.Add( @"\\[1-7][0-7]{2,}" ); // octal, three digits
			pb_escape.Add( @"\\N\{.+?(\} | $)" ); // Unicode name, ex.: \N{DIGIT ONE}
			pb_escape.Add( @"\\." );

			pb_escape.EndGroup( );

			//

			var pb = new PatternBuilder( );

			pb.BeginGroup( "comment" );

			pb.Add( @"\(\?\#.*?(\)|$)" ); // comment
			if( isVerbose ) pb.Add( @"\#.*?(\n|$)" ); // line-comment*/

			pb.EndGroup( );

			//

			pb.Add( @"\(\?P(?'name'<.*?(>|$))" );
			pb.Add( @"\(\?P=(?'name'.*?(\)|$))" );
			pb.Add( @"(?'name'\\[1-9][0-9]?(?![0-9]))" );

			//

			pb.Add( pb_escape.ToPattern( ) );

			//

			string char_group = @"( \[ \]? .*? (\]|$) )";

			pb.Add( char_group );

			// 

			return pb.ToRegex( );
		}


		Regex CreateHighlightingRegex( bool isVerbose )
		{
			var pb = new PatternBuilder( );

			pb.Add( @"(\(\?\#.*?(\)|$))" ); // comment
			if( isVerbose ) pb.Add( @"(\#[^\n]*)" ); // line comment
			pb.Add( @"\\[N]\{.*?(\}|$)" ); // (skip)
			pb.Add( @"(?'left_par'\()" ); // '('
			pb.Add( @"(?'right_par'\))" ); // ')'
			pb.Add( @"(?'left_brace'\{) (\d+ | \d*,\d*) ((?'right_brace'\})|$)" ); // '{...}'
			pb.Add( @"((?'left_bracket'\[) ]? (\\. | .)*? ((?'right_bracket'\])|$) )" ); // [...]
			pb.Add( @"\\." ); // '\...'

			return pb.ToRegex( );
		}


		static string GetVersion( )
		{
			try
			{
				return Matcher.GetPythonVersion( );
			}
			catch( Exception exc )
			{
				_ = exc;
				if( Debugger.IsAttached ) Debugger.Break( );

				return null;
			}
		}
	}
}
