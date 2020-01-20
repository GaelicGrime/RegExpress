using DotNetRegexEngineNs.Matches;
using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.SyntaxColouring;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;


namespace DotNetRegexEngineNs
{
	public class DotNetRegexEngine : IRegexEngine
	{
		readonly UCDotNetRegexOptions OptionsControl;

		static readonly Dictionary<RegexOptions, Regex> CachedColouringRegexes = new Dictionary<RegexOptions, Regex>( );
		static readonly Dictionary<RegexOptions, Regex> CachedHighlightingRegexes = new Dictionary<RegexOptions, Regex>( );


		static DotNetRegexEngine( )
		{
		}


		public DotNetRegexEngine( )
		{
			OptionsControl = new UCDotNetRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "DotNetRegex";

		public string Name => ".NET Regex";

		public event EventHandler OptionsChanged;


		public Control GetOptionsControl( )
		{
			return OptionsControl;
		}


		public RegexOptions RegexOptions => OptionsControl.CachedRegexOptions;


		public object SerializeOptions( )
		{
			return OptionsControl.ToSerialisableObject( );
		}


		public void DeserializeOptions( object obj )
		{
			OptionsControl.FromSerializableObject( obj );
		}


		public IMatcher ParsePattern( string pattern )
		{
			RegexOptions selected_options = OptionsControl.CachedRegexOptions;
			var regex = new Regex( pattern, selected_options );

			return new DotNetMatcher( regex );
		}


		public void ColourisePattern( ICancellable cnc, ColouredSegments colouredSegments, string pattern, Segment visibleSegment )
		{
			Regex regex = GetCachedColouringRegex( OptionsControl.CachedRegexOptions );

			foreach( Match m in regex.Matches( pattern ) )
			{
				Debug.Assert( m.Success );

				if( cnc.IsCancellationRequested ) return;

				// comments, '(?#...)'
				{
					var g = m.Groups["comment"];
					if( g.Success )
					{
						if( cnc.IsCancellationRequested ) return;

						var intersection = Segment.Intersection( visibleSegment, g.Index, g.Length );

						if( !intersection.IsEmpty )
						{
							colouredSegments.Comments.Add( intersection );
						}
					}
				}

				// end-on-line comments, '#...', only if 'IgnorePatternWhitespace' option is specified
				{
					var g = m.Groups["eol_comment"];
					if( g.Success )
					{
						if( cnc.IsCancellationRequested ) return;

						var intersection = Segment.Intersection( visibleSegment, g.Index, g.Length );

						if( !intersection.IsEmpty )
						{
							colouredSegments.Comments.Add( intersection );
						}
					}
				}

				// character groups, '[...]'
				//{
				//	var g = m.Groups["char_group"];
				//	if( g.Success )
				//	{

				//	}
				//}


				// escapes, '\...'
				{
					var g = m.Groups["escape"];
					if( g.Success )
					{
						if( cnc.IsCancellationRequested ) return;

						// we need captures because of '*?'
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

				// named groups, '(?<name>...' and "(?'name'...", including balancing groups
				{
					var g = m.Groups["name"];
					if( g.Success )
					{
						if( cnc.IsCancellationRequested ) return;

						var intersection = Segment.Intersection( visibleSegment, g.Index, g.Length );

						if( !intersection.IsEmpty )
						{
							colouredSegments.GroupNames.Add( intersection );
						}
					}
				}
			}
		}


		public Highlights HighlightPattern( ICancellable cnc, string pattern, int selectionStart, int selectionEnd, Segment visibleSegment )
		{
			Highlights highlights = new Highlights( );

			Regex regex = GetCachedHighlightingRegex( OptionsControl.CachedRegexOptions );

			var parentheses = new List<(int Index, char Value)>( );

			foreach( Match m in regex.Matches( pattern ) )
			{
				Debug.Assert( m.Success );

				if( cnc.IsCancellationRequested ) return null;

				// parantheses, '(' or ')'
				{
					var g = m.Groups["left_para"];
					if( g.Success )
					{
						parentheses.Add( (g.Index, '(') );
					}

					g = m.Groups["right_para"];
					if( g.Success )
					{
						parentheses.Add( (g.Index, ')') );
					}
				}

				if( cnc.IsCancellationRequested ) return null;

				// character groups, '[...]'
				{
					var g = m.Groups["char_group"];
					if( g.Success )
					{
						var normal_end = m.Groups["end"].Success;

						if( g.Index < selectionStart && ( normal_end ? selectionStart < g.Index + g.Length : selectionStart <= g.Index + g.Length ) )
						{
							if( visibleSegment.Contains( g.Index ) ) highlights.LeftBracket = new Segment( g.Index, 1 );

							if( normal_end )
							{
								var right = g.Index + g.Length - 1;

								if( visibleSegment.Contains( right ) ) highlights.RightBracket = new Segment( right, 1 );
							}
							break;
						}
					}
				}
			}

			var parentheses_at_left = parentheses.Where( g => g.Index < selectionStart ).ToArray( );
			if( cnc.IsCancellationRequested ) return null;

			var parentheses_at_right = parentheses.Where( g => g.Index >= selectionStart ).ToArray( );
			if( cnc.IsCancellationRequested ) return null;

			if( parentheses_at_left.Any( ) )
			{
				int n = 0;
				int found_i = -1;
				for( int i = parentheses_at_left.Length - 1; i >= 0; --i )
				{
					if( cnc.IsCancellationRequested ) break;

					var g = parentheses_at_left[i];
					if( g.Value == ')' ) --n;
					else if( g.Value == '(' ) ++n;
					if( n == +1 )
					{
						found_i = i;
						break;
					}
				}
				if( found_i >= 0 )
				{
					var g = parentheses_at_left[found_i];

					if( visibleSegment.Contains( g.Index ) ) highlights.LeftPara = new Segment( g.Index, 1 );
				}
			}

			if( cnc.IsCancellationRequested ) return null;

			if( parentheses_at_right.Any( ) )
			{
				int n = 0;
				int found_i = -1;
				for( int i = 0; i < parentheses_at_right.Length; ++i )
				{
					if( cnc.IsCancellationRequested ) break;

					var g = parentheses_at_right[i];
					if( g.Value == '(' ) --n;
					else if( g.Value == ')' ) ++n;
					if( n == +1 )
					{
						found_i = i;
						break;
					}
				}
				if( found_i >= 0 )
				{
					var g = parentheses_at_right[found_i];

					if( visibleSegment.Contains( g.Index ) ) highlights.RightPara = new Segment( g.Index, 1 );
				}
			}

			if( cnc.IsCancellationRequested ) return null;

			return highlights;
		}

		#endregion IRegexEngine


		private void OptionsControl_Changed( object sender, EventArgs e )
		{
			OptionsChanged?.Invoke( this, null );
		}


		static Regex GetCachedColouringRegex( RegexOptions options )
		{
			options &= RegexOptions.IgnorePatternWhitespace; // filter unneeded flags

			lock( CachedColouringRegexes )
			{
				if( CachedColouringRegexes.TryGetValue( options, out Regex regex ) ) return regex;

				const string CommentPattern = @"(?'comment'\(\?\#.*?(\)|$))"; // including incomplete
				const string EolCommentPattern = @"(?'eol_comment'\#[^\n]*)";
				const string CharGroupPattern = @"(?'char_group'\[\]?(<<INTERIOR>>|.)*?(\]|$))"; // including incomplete
				const string EscapesPattern = @"(?'escape'
\\[0-7]{2,3} | 
\\x[0-9A-Fa-f]{1,2} | 
\\c[A-Za-z] | 
\\u[0-9A-Fa-f]{1,4} | 
\\(p|P)\{([A-Za-z]+\})? | 
\\k<([A-Za-z]+>)? |
\\.
)"; // including incomplete '\x', '\u', '\p', '\k'
				const string NamedGroupPattern = @"\(\?(?'name'((?'a'')|<)\p{L}\w*(-\p{L}\w*)?(?(a)'|>))"; // (balancing groups covered too)

				string pattern;

				if( options.HasFlag( RegexOptions.IgnorePatternWhitespace ) )
				{
					pattern =
						@"(?nsx)(" + Environment.NewLine +
							CommentPattern + " |" + Environment.NewLine +
							EolCommentPattern + " |" + Environment.NewLine +
							CharGroupPattern.Replace( "<<INTERIOR>>", EscapesPattern ) + " |" + Environment.NewLine +
							EscapesPattern + " |" + Environment.NewLine +
							NamedGroupPattern + " |" + Environment.NewLine +
							".(?!)" + Environment.NewLine +
						")";
				}
				else
				{
					pattern =
						@"(?nsx)(" + Environment.NewLine +
							CommentPattern + " |" + Environment.NewLine +
							//EolCommentPattern + " |" + Environment.NewLine +
							CharGroupPattern.Replace( "<<INTERIOR>>", EscapesPattern ) + " |" + Environment.NewLine +
							EscapesPattern + " |" + Environment.NewLine +
							NamedGroupPattern + " |" + Environment.NewLine +
							".(?!)" + Environment.NewLine +
						")";
				}

				regex = new Regex( pattern, RegexOptions.Compiled );

				CachedColouringRegexes.Add( options, regex );

				return regex;
			}
		}

		static Regex GetCachedHighlightingRegex( RegexOptions options )
		{
			options &= RegexOptions.IgnorePatternWhitespace; // filter unneeded flags

			lock( CachedHighlightingRegexes )
			{
				if( CachedHighlightingRegexes.TryGetValue( options, out Regex regex ) ) return regex;

				const string HighlightPatternIgnoreWhitespace = @"(?nsx)
(
(\(\?\#.*?(\)|$)) |
(\#[^\n]*) |
(?'left_para'\() |
(?'right_para'\)) |
(?'char_group'\[(\\.|.)*?(\](?'end')|$)) |
(\\.)
)
";
				const string HighlightPatternNoIgnoreWhitespace = @"(?nsx)
(
(\(\?\#.*?(\)|$)) |
#(\#[^\n]*) |
(?'left_para'\() |
(?'right_para'\)) |
(?'char_group'\[(\\.|.)*?(\](?'end')|$)) |
(\\.)
)
";

				string pattern;

				if( options.HasFlag( RegexOptions.IgnorePatternWhitespace ) )
				{
					pattern = HighlightPatternIgnoreWhitespace;
				}
				else
				{
					pattern = HighlightPatternNoIgnoreWhitespace;
				}

				regex = new Regex( pattern, RegexOptions.Compiled );

				CachedHighlightingRegexes.Add( options, regex );

				return regex;
			}
		}
	}
}

