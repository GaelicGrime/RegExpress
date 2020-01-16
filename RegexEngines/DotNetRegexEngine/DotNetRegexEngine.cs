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


		const string IgnoreWhitespacePattern = @"(?nsx)
(
(?'comment'\(\?\#.*?(\)|(?'unfinished'$))) |
(?'char_group'\[(\\.|.)*?(\]|(?'unfinished'$))) |
(?'eol_comment'\#[^\n]*) |
\\. | .
)+
";

		const string NoIgnoreWhitespacePattern = @"(?nsx)
(
(?'comment'\(\?\#.*?(\)|(?'unfinished'$))) |
(?'char_group'\[(\\.|.)*?(\]|(?'unfinished'$))) |
(?'eol_comment'(?!)) 
\\. | .
)+
";

		const string EscapesPattern = @"(?nsx)
(?'escape'
\\[0-7]{2,3} | 
\\x[0-9A-F]{2} | 
\\c[A-Z] | 
\\u[0-9A-F]{4} | 
\\p\{[A-Z]+\} | 
\\k<[A-Z]+> | 
\\.
)";

		const string NamedGroupPattern = @"(?nsx)
\(\?(?'name'((?'a'')|<)\p{L}\w*(-\p{L}\w*)?(?(a)'|>))
"; // (balancing groups covered too)

		readonly Regex ReIgnorePatternWhitespace = new Regex( IgnoreWhitespacePattern, RegexOptions.Compiled );
		readonly Regex ReNoIgnorePatternWhitespace = new Regex( NoIgnoreWhitespacePattern, RegexOptions.Compiled );
		readonly Regex ReEscapes = new Regex( EscapesPattern, RegexOptions.Compiled );
		readonly Regex ReEscapesAndNamedGroups = new Regex( $"(?nsx)({EscapesPattern})|({NamedGroupPattern})", RegexOptions.Compiled );


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
			Regex re = ignore_pattern_whitespaces ? ReIgnorePatternWhitespace : ReNoIgnorePatternWhitespace;

			var uncovered_segments = new List<Segment> { new Segment( 0, pattern.Length ) };

			foreach( Match m in re.Matches( pattern ) ) // (only one)
			{
				Debug.Assert( m.Success );

				if( cnc.IsCancellationRequested ) return;

				// comments, '(?#...)'
				{
					var g = m.Groups["comment"];
					if( g.Success )
					{
						foreach( Capture c in g.Captures )
						{
							if( cnc.IsCancellationRequested ) return;

							Segment.Exclude( uncovered_segments, c.Index, c.Length );

							var intersection = Segment.Intersection( visibleSegment, c.Index, c.Length );

							if( !intersection.IsEmpty )
							{
								colouredSegments.Comments.Add( intersection );
							}
						}
					}
				}

				// end-on-line comments, '#...'
				{
					var g = m.Groups["eol_comment"];
					if( g.Success )
					{
						foreach( Capture c in g.Captures )
						{
							if( cnc.IsCancellationRequested ) return;

							Segment.Exclude( uncovered_segments, c.Index, c.Length );

							var intersection = Segment.Intersection( visibleSegment, c.Index, c.Length );

							if( !intersection.IsEmpty )
							{
								colouredSegments.Comments.Add( intersection );
							}
						}
					}
				}

				// character groups, '[...]'
				{
					var g = m.Groups["char_group"];
					if( g.Success )
					{
						foreach( Capture c in g.Captures )
						{
							if( cnc.IsCancellationRequested ) return;

							Segment.Exclude( uncovered_segments, c.Index, c.Length );

							string text = pattern.Substring( c.Index + 1, c.Length - 2 );

							ColouriseEscapes( cnc, colouredSegments, text, c.Index + 1, visibleSegment );
						}
					}
				}

				// process uncovered segments
				{
					foreach( var s in uncovered_segments )
					{
						if( cnc.IsCancellationRequested ) return;

						if( s.IsEmpty ) continue;

						string text = pattern.Substring( s.Index, s.Length );

						ColouriseEscapesAndNamedGroups( cnc, colouredSegments, text, s.Index, visibleSegment );
					}
				}
			}
		}

		#endregion IRegexEngine


		private void ColouriseEscapes( ICancellable cnc, ColouredSegments colouredSegments, string text, int index, Segment visibleSegment )
		{
			var end = visibleSegment.End;

			foreach( Match m in ReEscapes.Matches( text ) )
			{
				if( cnc.IsCancellationRequested ) return;

				if( m.Index + index > end ) break;

				var intersection = Segment.Intersection( visibleSegment, m.Index + index, m.Length );

				if( !intersection.IsEmpty )
				{
					colouredSegments.Escapes.Add( intersection );
				}
			}
		}


		private void ColouriseEscapesAndNamedGroups( ICancellable cnc, ColouredSegments colouredSegments, string text, int index, Segment visibleSegment )
		{
			var end = visibleSegment.End;

			foreach( Match m in ReEscapesAndNamedGroups.Matches( text ) )
			{
				if( cnc.IsCancellationRequested ) return;

				if( m.Index + index > end ) break;

				var name = m.Groups["name"];
				if( name.Success )
				{
					var intersection = Segment.Intersection( visibleSegment, name.Index + index, name.Length );

					if( !intersection.IsEmpty )
					{
						colouredSegments.GroupNames.Add( intersection );
					}
				}
				else
				{
					var escape = m.Groups["escape"];
					if( escape.Success )
					{
						var intersection = Segment.Intersection( visibleSegment, m.Index + index, m.Length );

						if( !intersection.IsEmpty )
						{
							colouredSegments.Escapes.Add( intersection );
						}
					}
				}
			}
		}


		private void OptionsControl_Changed( object sender, EventArgs e )
		{
			OptionsChanged?.Invoke( this, null );
		}


	}
}
