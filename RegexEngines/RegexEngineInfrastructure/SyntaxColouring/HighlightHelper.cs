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
		public static void CommonHighlighting( ICancellable cnc, Highlights highlights, string pattern, int selectionStart, int selectionEnd, Segment visibleSegment,
			Regex regex, int paraSize )
		{
			var parentheses = new List<(int Index, char Value)>( );

			foreach( Match m in regex.Matches( pattern ) )
			{
				Debug.Assert( m.Success );

				if( cnc.IsCancellationRequested ) return;

				// parantheses, '(' or ')'
				{
					var g = m.Groups["left_para"];
					if( g.Success )
					{
						parentheses.Add( (g.Index, '(') );

						continue;
					}

					g = m.Groups["right_para"];
					if( g.Success )
					{
						parentheses.Add( (g.Index, ')') );

						continue;
					}
				}

				if( cnc.IsCancellationRequested ) return;

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
						}

						continue;
					}
				}

				if( cnc.IsCancellationRequested ) return;

				// range, '{...}'
				{
					var g = m.Groups["range"];
					if( g.Success )
					{
						var normal_end = m.Groups["end"].Success;

						if( g.Index < selectionStart && ( normal_end ? selectionStart < g.Index + g.Length : selectionStart <= g.Index + g.Length ) )
						{
							var s = new Segment( g.Index, paraSize );

							if( visibleSegment.Intersects( s ) ) highlights.LeftCurlyBracket = s;

							if( normal_end )
							{
								var right = g.Index + g.Length - paraSize;
								s = new Segment( right, paraSize );

								if( visibleSegment.Intersects( s ) ) highlights.RightCurlyBracket = s;
							}
						}

						continue;
					}
				}
			}


			var parentheses_at_left = parentheses.Where( g => ( g.Value == '(' && selectionStart > g.Index ) || ( g.Value == ')' && selectionStart > g.Index + ( paraSize - 1 ) ) ).ToArray( );
			if( cnc.IsCancellationRequested ) return;

			var parentheses_at_right = parentheses.Where( g => ( g.Value == '(' && selectionStart <= g.Index ) || ( g.Value == ')' && selectionStart <= g.Index + ( paraSize - 1 ) ) ).ToArray( );
			if( cnc.IsCancellationRequested ) return;

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
					var s = new Segment( g.Index, paraSize );

					if( visibleSegment.Intersects( s ) ) highlights.LeftPara = s;
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
					var s = new Segment( g.Index, paraSize );

					if( visibleSegment.Intersects( s ) ) highlights.RightPara = s;
				}
			}
		}
	}
}
