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

		const string HighlightPatternIgnoreWhitespace = @"(?nsx)
(
(\(\?\#.*?(\)|$)) |
(\#[^\n]*) |
(?'left_para'\() |
(?'right_para'\)) |
(?'char_group'\[(\\.|.)*?(\]|$)) |
(\\.)
)
";
		const string HighlightPatternNoIgnoreWhitespace = @"(?nsx)
(
(\(\?\#.*?(\)|$)) |
#(\#[^\n]*) |
(?'left_para'\() |
(?'right_para'\)) |
(?'char_group'\[(\\.|.)*?(\]|$)) |
(\\.)
)
";

		readonly static string CombinedPatternIgnoreWhitespaces;
		readonly static string CombinedPatternNoIgnoreWhitespaces;

		readonly static Regex CombinedRegexIgnoreWhitespaces;
		readonly static Regex CombinedRegexNoIgnoreWhitespaces;

		readonly static Regex HighlightRegexIgnoreWhitespaces;
		readonly static Regex HighlightRegexNoIgnoreWhitespaces;



		static DotNetRegexEngine( )
		{
			CombinedPatternIgnoreWhitespaces =
				@"(?nsx)(" + Environment.NewLine +
					 CommentPattern + " |" + Environment.NewLine +
					 EolCommentPattern + " |" + Environment.NewLine +
					 CharGroupPattern.Replace( "<<INTERIOR>>", EscapesPattern ) + " |" + Environment.NewLine +
					 EscapesPattern + " |" + Environment.NewLine +
					 NamedGroupPattern + " |" + Environment.NewLine +
					 "(?>.(?!))" + Environment.NewLine +
				")";

			CombinedPatternNoIgnoreWhitespaces =
				@"(?nsx)(" + Environment.NewLine +
					 CommentPattern + " |" + Environment.NewLine +
					 //EolCommentPattern + " |" + Environment.NewLine +
					 CharGroupPattern.Replace( "<<INTERIOR>>", EscapesPattern ) + " |" + Environment.NewLine +
					 EscapesPattern + " |" + Environment.NewLine +
					 NamedGroupPattern + " |" + Environment.NewLine +
					 "(?>.(?!))" + Environment.NewLine +
				")";

			CombinedRegexIgnoreWhitespaces = new Regex( CombinedPatternIgnoreWhitespaces, RegexOptions.Compiled );
			CombinedRegexNoIgnoreWhitespaces = new Regex( CombinedPatternNoIgnoreWhitespaces, RegexOptions.Compiled );

			HighlightRegexIgnoreWhitespaces = new Regex( HighlightPatternIgnoreWhitespace, RegexOptions.Compiled );
			HighlightRegexNoIgnoreWhitespaces = new Regex( HighlightPatternNoIgnoreWhitespace, RegexOptions.Compiled );
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
			bool ignore_pattern_whitespaces = OptionsControl.CachedRegexOptions.HasFlag( RegexOptions.IgnorePatternWhitespace );
			Regex regex = ignore_pattern_whitespaces ? CombinedRegexIgnoreWhitespaces : CombinedRegexNoIgnoreWhitespaces;

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

						var intersection = Segment.Intersection( visibleSegment, g.Index, g.Length );

						if( !intersection.IsEmpty )
						{
							colouredSegments.Escapes.Add( intersection );
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

			bool ignore_pattern_whitespaces = OptionsControl.CachedRegexOptions.HasFlag( RegexOptions.IgnorePatternWhitespace );
			Regex regex = ignore_pattern_whitespaces ? HighlightRegexIgnoreWhitespaces : HighlightRegexNoIgnoreWhitespaces;

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
						if( g.Index < selectionStart && selectionStart < g.Index + g.Length )
						{
							if( visibleSegment.Contains( g.Index ) ) highlights.LeftBracket = new Segment( g.Index, 1 );

							var right = g.Value.EndsWith( "]" ) ? g.Index + g.Length - 1 : -1;
							if( right >= 0 && visibleSegment.Contains( right ) ) highlights.RightBracket = new Segment( right, 1 );

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
	}
}
