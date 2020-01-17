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
		const string CharGroupPattern = @"(?'char_group'\[(<<INTERIOR>>|.)*?(\]|$))"; // including incomplete
		const string EscapesPattern = @"(?'escape'
\\[0-7]{2,3} | 
\\x[0-9A-Fa-f]{2} | 
\\c[A-Za-z] | 
\\u[0-9A-Fa-f]{4} | 
\\(p|P)\{([A-Za-z]+\})? | 
\\k<([A-Za-z]+>)? |
\\.
)"; // including incomplete '\p' and '\k'
		const string NamedGroupPattern = @"\(\?(?'name'((?'a'')|<)\p{L}\w*(-\p{L}\w*)?(?(a)'|>))"; // (balancing groups covered too)

		readonly static string CombinedPatternIgnoreWhitespaces;
		readonly static string CombinedPatternNoIgnoreWhitespaces;

		readonly static Regex CombinedRegexIgnoreWhitespaces;
		readonly static Regex CombinedRegexNoIgnoreWhitespaces;


		static DotNetRegexEngine( )
		{
			CombinedPatternIgnoreWhitespaces =
				@"(?nsx)(" + Environment.NewLine +
					 CommentPattern + " |" + Environment.NewLine +
					 EolCommentPattern + " |" + Environment.NewLine +
					 CharGroupPattern.Replace( "<<INTERIOR>>", EscapesPattern ) + " |" + Environment.NewLine +
					 EscapesPattern +" |" + Environment.NewLine +
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
			Regex re = ignore_pattern_whitespaces ? CombinedRegexIgnoreWhitespaces : CombinedRegexNoIgnoreWhitespaces;

			foreach( Match m in re.Matches( pattern ) )
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


		public Highlights GetHighlightsInPattern( ICancellable cnc, string pattern, int startSelection, int endSelection, Segment visibleSegment )
		{
			Highlights highlights = new Highlights( );

			bool ignore_pattern_whitespaces = OptionsControl.CachedRegexOptions.HasFlag( RegexOptions.IgnorePatternWhitespace );
			Regex re = ignore_pattern_whitespaces ? CombinedRegexIgnoreWhitespaces : CombinedRegexNoIgnoreWhitespaces;

			foreach( Match m in re.Matches( pattern ) )
			{
				Debug.Assert( m.Success );

				if( cnc.IsCancellationRequested ) return null;


				// character groups, '[...]'
				{
					var g = m.Groups["char_group"];
					if( g.Success )
					{
						if( cnc.IsCancellationRequested ) return null;

						if( g.Index < startSelection && startSelection <= g.Index + g.Length )
						{
							highlights.LeftBracket = g.Index;

							break;
						}
					}
				}
			}

			return highlights;
		}

		#endregion IRegexEngine


		private void OptionsControl_Changed( object sender, EventArgs e )
		{
			OptionsChanged?.Invoke( this, null );
		}
	}
}
