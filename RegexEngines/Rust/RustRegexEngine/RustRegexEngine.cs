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


namespace RustRegexEngineNs
{
	public class RustRegexEngine : IRegexEngine
	{
		readonly UCRustRegexOptions OptionsControl;
		static readonly Lazy<string> LazyVersion = new Lazy<string>( GetVersion );

		static readonly Dictionary<string, Regex> CachedColouringRegexes = new Dictionary<string, Regex>( );
		static readonly Dictionary<string, Regex> CachedHighlightingRegexes = new Dictionary<string, Regex>( );


		public RustRegexEngine( )
		{
			OptionsControl = new UCRustRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "RustRegex";

		public string Name => "Rust";

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
			RustRegexOptions options = OptionsControl.GetSelectedOptions( );
			var json = JsonSerializer.Serialize( options );

			return new[] { $"json:{json}" };
		}


		public void ImportOptions( string[] options )
		{
			var json = options.FirstOrDefault( o => o.StartsWith( "json:" ) )?.Substring( "json:".Length );

			RustRegexOptions options_obj;
			if( string.IsNullOrWhiteSpace( json ) )
			{
				options_obj = new RustRegexOptions( );
			}
			else
			{
				options_obj = JsonSerializer.Deserialize<RustRegexOptions>( json );
			}

			OptionsControl.SetSelectedOptions( options_obj );
		}


		public IMatcher ParsePattern( string pattern )
		{
			RustRegexOptions options = OptionsControl.GetSelectedOptions( );

			return new RustMatcher( pattern, options );
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

				// comment, '#...'
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
			RustRegexOptions options = OptionsControl.GetSelectedOptions( );
			string key = $"{options.@struct}\x1F{options.octal}\x1F{options.unicode}\x1F{options.ignore_whitespace}";

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
			RustRegexOptions options = OptionsControl.GetSelectedOptions( );
			string key = $"{options.@struct}\x1F{options.unicode}\x1F{options.ignore_whitespace}";

			lock( CachedHighlightingRegexes )
			{
				if( CachedHighlightingRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateHighlightingRegex( options );

				CachedHighlightingRegexes.Add( key, regex );

				return regex;
			}
		}


		Regex CreateColouringRegex( RustRegexOptions options )
		{
			bool is_regex = options.@struct == "Regex";
			bool is_regex_builder = options.@struct == "RegexBuilder";

			var pb_escape = new PatternBuilder( );

			pb_escape.BeginGroup( "escape" );

			if( is_regex || ( is_regex_builder && options.unicode ) )
			{
				pb_escape.Add( @"\\[pP]\{.*?(\}|$)" ); // Unicode character class (general category or script)
				pb_escape.Add( @"\\[pP].?" ); // One-letter name Unicode character class
			}

			if( is_regex_builder && options.octal )
			{
				pb_escape.Add( @"\\[0-7]{1,3}" ); // octal character code (up to three digits) (when enabled)
			}

			pb_escape.Add( @"\\x\{[0-9a-fA-F]*(\}|$)?" ); // any hex character code corresponding to a Unicode code point
			pb_escape.Add( @"\\x[0-9a-fA-F]{0,2}" ); // hex character code (exactly two digits)

			// (only 2 digits if no 'options.unicode'
			pb_escape.Add( @"\\u\{[0-9a-fA-F]*(\}|$)?" ); // any hex character code corresponding to a Unicode code point
			pb_escape.Add( @"\\u[0-9a-fA-F]{0,4}" ); // hex character code (exactly four digits)
			pb_escape.Add( @"\\U\{[0-9a-fA-F]*(\}|$)?" ); // any hex character code corresponding to a Unicode code point
			pb_escape.Add( @"\\U[0-9a-fA-F]{0,8}" ); // hex character code (exactly eight digits)

			string any_esc = "";

			if( !( is_regex || ( is_regex_builder && options.unicode ) ) ) any_esc += @"(?!\\[pP])";
			if( !( is_regex_builder && options.octal ) ) any_esc += @"(?!\\[0-7])";

			any_esc += @"\\.";

			pb_escape.Add( any_esc );

			pb_escape.EndGroup( );


			var pb = new PatternBuilder( );

			pb.BeginGroup( "comment" );

			if( is_regex_builder && options.ignore_whitespace )
			{
				pb.Add( @"\#.*?(\n|$)" ); // line-comment
			}

			pb.EndGroup( );

			pb.Add( @"\(\?P(?'name'<.*?(>|$))" );

			{
				// (nested groups: https://stackoverflow.com/questions/546433/regular-expression-to-match-balanced-parentheses)

				string posix_bracket = @"(?'escape'\[:.*?(:\]|$))"; // [:...:], use escape colour

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


		Regex CreateHighlightingRegex( RustRegexOptions options )
		{
			bool is_regex_builder = options.@struct == "RegexBuilder";

			var pb = new PatternBuilder( );

			if( is_regex_builder && options.ignore_whitespace ) pb.Add( @"\#.*?(\n|$)" ); // line-comment

			pb.Add( @"(?'left_par'\()" ); // '('
			pb.Add( @"(?'right_par'\))" ); // ')'

			pb.Add( @"\\[xuU]\{.*?(\}|$)" ); // \x{7HHHHHHH ...} etc.

			if( is_regex_builder && options.unicode ) pb.Add( @"\\[pP]\{.*?(\} | $)" ); // property

			pb.Add( @"(?'left_brace'\{) (\d+(,\d*)? | ,\d+) ((?'right_brace'\})|$)" ); // '{...}'

			string posix_bracket = @"(\[:.*?(:\]|$))"; // [:...:]

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
				return RustMatcher.GetRustVersion( NonCancellable.Instance );
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
