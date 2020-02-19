using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure.SyntaxColouring
{
	public static class HighlightHelper
	{
		enum ParKindEnum
		{
			Left,
			Right
		}

		struct Par
		{
			public readonly int Index;
			private readonly ParKindEnum Kind;

			public Par( int index, ParKindEnum kind )
			{
				Index = index;
				Kind = kind;
			}

			public bool IsLeft => Kind == ParKindEnum.Left;
			public bool IsRight => Kind == ParKindEnum.Right;
		}


		public static void CommonHighlighting( ICancellable cnc, Highlights highlights, string pattern, int selectionStart, int selectionEnd, Segment visibleSegment,
			Regex regex, int paraSize, int bracketSize )
		{
			var parentheses = new List<Par>( );
			var brackets = new List<Par>( );

			foreach( Match m in regex.Matches( pattern ) )
			{
				Debug.Assert( m.Success );

				if( cnc.IsCancellationRequested ) return;

				// parantheses, '(', ')', '\(', '\)' depending on syntax
				{
					var g = m.Groups["left_par"];
					if( g.Success )
					{
						parentheses.Add( new Par( g.Index, ParKindEnum.Left ) );

						continue;
					}

					g = m.Groups["right_par"];
					if( g.Success )
					{
						parentheses.Add( new Par( g.Index, ParKindEnum.Right ) );

						continue;
					}
				}

				if( cnc.IsCancellationRequested ) return;

				// brackets, '[' or ']'
				{
					var g = m.Groups["left_bracket"];
					if( g.Success )
					{
						foreach( Capture c in g.Captures )
						{
							if( cnc.IsCancellationRequested ) return;

							brackets.Add( new Par( c.Index, ParKindEnum.Left ) );
						}
					}

					g = m.Groups["right_bracket"];
					if( g.Success )
					{
						foreach( Capture c in g.Captures )
						{
							if( cnc.IsCancellationRequested ) return;

							brackets.Add( new Par( c.Index, ParKindEnum.Right ) );
						}
					}
				}

				if( cnc.IsCancellationRequested ) return;

				// range, '{...}' or '\{...\}', depending on syntax
				{
					var left_brace = m.Groups["left_brace"];
					var right_brace = m.Groups["right_brace"];

					if( left_brace.Success )
					{
						if( left_brace.Index < selectionStart &&
							( !right_brace.Success || selectionStart < right_brace.Index + right_brace.Length ) )
						{
							var s = new Segment( left_brace.Index, left_brace.Length );

							if( visibleSegment.Intersects( s ) ) highlights.LeftCurlyBrace = s;
						}
					}

					if( cnc.IsCancellationRequested ) return;

					if( right_brace.Success )
					{
						if( ( !left_brace.Success || left_brace.Index < selectionStart ) &&
							selectionStart < right_brace.Index + right_brace.Length )
						{
							var s = new Segment( right_brace.Index, right_brace.Length );

							if( visibleSegment.Intersects( s ) ) highlights.RightCurlyBrace = s;
						}
					}
				}
			}

			ProcessParenthesesOrBrackets( cnc, highlights, selectionStart, visibleSegment, paraSize, parentheses, isBracket: false );
			ProcessParenthesesOrBrackets( cnc, highlights, selectionStart, visibleSegment, bracketSize, brackets, isBracket: true );
		}


		static void ProcessParenthesesOrBrackets( ICancellable cnc, Highlights highlights, int selectionStart, Segment visibleSegment, int size, List<Par> parentheses, bool isBracket )
		{
			var parentheses_at_left = parentheses.Where( g => ( g.IsLeft && selectionStart > g.Index ) || ( g.IsRight && selectionStart > g.Index + ( size - 1 ) ) ).ToArray( );
			if( cnc.IsCancellationRequested ) return;

			var parentheses_at_right = parentheses.Where( g => ( g.IsLeft && selectionStart <= g.Index ) || ( g.IsRight && selectionStart <= g.Index + ( size - 1 ) ) ).ToArray( );
			if( cnc.IsCancellationRequested ) return;

			if( parentheses_at_left.Any( ) )
			{
				int n = 0;
				int found_i = -1;
				for( int i = parentheses_at_left.Length - 1; i >= 0; --i )
				{
					if( cnc.IsCancellationRequested ) break;

					var p = parentheses_at_left[i];
					if( p.IsRight ) --n;
					else if( p.IsLeft ) ++n;
					if( n == +1 )
					{
						found_i = i;
						break;
					}
				}
				if( found_i >= 0 )
				{
					var p = parentheses_at_left[found_i];
					var s = new Segment( p.Index, size );

					if( isBracket )
						highlights.LeftBracket = s;
					else
						highlights.LeftPar = s;
				}
			}

			if( cnc.IsCancellationRequested ) return;

			if( parentheses_at_right.Any( ) )
			{
				int n = 0;
				int found_i = -1;
				for( int i = 0; i < parentheses_at_right.Length; ++i )
				{
					if( cnc.IsCancellationRequested ) break;

					var p = parentheses_at_right[i];
					if( p.IsLeft ) --n;
					else if( p.IsRight ) ++n;
					if( n == +1 )
					{
						found_i = i;
						break;
					}
				}
				if( found_i >= 0 )
				{
					var p = parentheses_at_right[found_i];
					var s = new Segment( p.Index, size );

					if( visibleSegment.Intersects( s ) )
					{
						if( isBracket )
							highlights.RightBracket = s;
						else
							highlights.RightPar = s;
					}
				}
			}
		}
	}
}
