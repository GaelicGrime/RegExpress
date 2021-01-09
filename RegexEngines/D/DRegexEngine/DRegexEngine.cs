using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.SyntaxColouring;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;


namespace DRegexEngineNs
{
	public class DRegexEngine : IRegexEngine
	{
		readonly UCDRegexOptions OptionsControl;
		static readonly Lazy<string> LazyVersion = new Lazy<string>( GetVersion );

		static readonly Dictionary<string, Regex> CachedColouringRegexes = new Dictionary<string, Regex>( );
		static readonly Dictionary<string, Regex> CachedHighlightingRegexes = new Dictionary<string, Regex>( );


		public DRegexEngine( )
		{
			OptionsControl = new UCDRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "DRegex";

		public string Name => "D";

		public string EngineVersion => LazyVersion.Value;

		public RegexEngineCapabilityEnum Capabilities => RegexEngineCapabilityEnum.NoCaptures;

		public string NoteForCaptures => null;

		public event RegexEngineOptionsChanged OptionsChanged;


		public Control GetOptionsControl( )
		{
			return OptionsControl;
		}


		public string[] ExportOptions( )
		{
			DRegexOptions options = OptionsControl.GetSelectedOptions( );
			var json = JsonSerializer.Serialize( options );

			return new[] { $"json:{json}" };
		}


		public void ImportOptions( string[] options )
		{
			var json = options.FirstOrDefault( o => o.StartsWith( "json:" ) )?.Substring( "json:".Length );

			DRegexOptions options_obj;
			if( string.IsNullOrWhiteSpace( json ) )
			{
				options_obj = new DRegexOptions( );
			}
			else
			{
				options_obj = JsonSerializer.Deserialize<DRegexOptions>( json );
			}

			OptionsControl.SetSelectedOptions( options_obj );
		}


		public IMatcher ParsePattern( string pattern )
		{
			DRegexOptions options = OptionsControl.GetSelectedOptions( );

			return new DMatcher( pattern, options );
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

				// comment, '(?#...)'
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

				// named groups
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
			const int par_size = 1;
			const int bracket_size = 1;

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
			DRegexOptions options = OptionsControl.GetSelectedOptions( );
			string key = "";

			lock( CachedColouringRegexes )
			{
				if( CachedColouringRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateColouringRegex( options );

				CachedColouringRegexes.Add( key, regex );

				return regex;
			}
		}


		Regex GetCachedHighlightingRegex( )
		{
			DRegexOptions options = OptionsControl.GetSelectedOptions( );
			string key = "";

			lock( CachedHighlightingRegexes )
			{
				if( CachedHighlightingRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateHighlightingRegex( options );

				CachedHighlightingRegexes.Add( key, regex );

				return regex;
			}
		}


		Regex CreateColouringRegex( DRegexOptions options )
		{
			var pb_escape = new PatternBuilder( );

			pb_escape.BeginGroup( "escape" );

			pb_escape.Add( @"\\c[A-Za-z]" ); // Matches the control character corresponding to letter C
			pb_escape.Add( @"\\x[0-9a-fA-F]{0,2}" ); // Matches a character with hexadecimal value of XX.
			pb_escape.Add( @"\\u[0-9a-fA-F]{0,4}" ); // Matches a character with hexadecimal value of XXXX.
			pb_escape.Add( @"\\U[0-9a-fA-F]{0,8}" ); // Matches a character with hexadecimal value of YYYYYY.

			pb_escape.Add( @"\\[pP]\{.*?(\}|$)" );
			pb_escape.Add( @"\\[pP].?" );

			pb_escape.Add( @"\\." );

			pb_escape.EndGroup( );

			var pb = new PatternBuilder( );

			pb.BeginGroup( "comment" );
			pb.Add( @"\(\?\#.*?(\)|$)" ); // An inline comment that is ignored while matching.
			pb.EndGroup( );

			pb.Add( @"\(\?P(?'name'<.*?(>|$))" ); // Matches named subexpression regex labeling it with name 'name'. 

			{
				// (nested groups: https://stackoverflow.com/questions/546433/regular-expression-to-match-balanced-parentheses)

				string posix_bracket = ""; // Not supported: @"(?'escape'\[:.*?(:\]|$))"; // [:...:], use escape colour

				pb.Add( $@"
						\[ 
						\]?
						(?> {posix_bracket}{( posix_bracket.Length == 0 ? "" : " |" )} \[(?<c>) | ({pb_escape.ToPattern( )} | [^\[\]])+ | \](?<-c>))*
						(?(c)(?!))
						\]
						" );

			}

			pb.Add( pb_escape.ToPattern( ) );

			return pb.ToRegex( );

		}


		Regex CreateHighlightingRegex( DRegexOptions options )
		{
			var pb = new PatternBuilder( );

			pb.Add( @"\(\?\#.*?(\)|$)" ); // inline comment

			pb.Add( @"(?'left_par'\()" ); // '('
			pb.Add( @"(?'right_par'\))" ); // ')'

			pb.Add( @"\\[pP]\{.*?(\} | $)" ); // property

			pb.Add( @"(?'left_brace'\{) (\d+(,\d*)? | ,\d+) ((?'right_brace'\})|$)" ); // '{...}'

			string posix_bracket = ""; // Not supported: @"(\[:.*?(:\]|$))"; // [:...:]

			pb.Add( $@"
						(?'left_bracket'\[)
						\]?
						(?> {posix_bracket}{( posix_bracket.Length == 0 ? "" : " |" )} (?'left_bracket'\[)(?<c>) | (\\. | [^\[\]])+ | (?'right_bracket'\])(?<-c>))*
						(?(c)(?!))
						(?'right_bracket'\])?
						|
						(?'right_bracket'\])
						" );

			pb.Add( @"\\." ); // '\...'

			return pb.ToRegex( );
		}


		static string GetVersion( )
		{
			try
			{
				return DMatcher.GetDVersion( NonCancellable.Instance );
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
