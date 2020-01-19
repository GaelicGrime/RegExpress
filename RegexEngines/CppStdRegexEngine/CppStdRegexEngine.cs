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


namespace CppStdRegexEngineNs
{
	public class CppStdRegexEngine : IRegexEngine
	{
		readonly UCCppStdRegexOptions OptionsControl;

		public CppStdRegexEngine( )
		{
			OptionsControl = new UCCppStdRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "CppStdRegex";

		public string Name => "C++ STL <regex>";

		public event EventHandler OptionsChanged;


		public Control GetOptionsControl( )
		{
			return OptionsControl;
		}


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
			string[] selected_options = OptionsControl.CachedOptions;

			return new CppStdRegexInterop.CppMatcher( pattern, selected_options );
		}


		public void ColourisePattern( ICancellable cnc, ColouredSegments colouredSegments, string pattern, Segment visibleSegment )
		{
			GrammarEnum grammar = OptionsControl.GetGrammar( );

			//StringBuilder sb = new StringBuilder( @"(?nsx)(?'escape'" );

			// TODO: cache

			string escape = @"(?'escape'";

			if( grammar == GrammarEnum.ECMAScript ) escape += @"\\c[A-Za-z] | ";
			if( grammar == GrammarEnum.ECMAScript ) escape += @"\\x[0-9A-Fa-f]{1,2} | "; // (two digits required)
			if( grammar == GrammarEnum.awk ) escape += @"\\[0-7]{1,3} | "; // octal code
			if( grammar == GrammarEnum.ECMAScript ) escape += @"\\u[0-9A-Fa-f]{1,4} | "; // (four digits required)
			escape += @"\\.)";

			string @class = @"(?'class' \[(?'c'[:=.]) .*? (\k<c>\] | $) )";

			string char_group = @"( \[ (" + @class + " | " + escape + " | . " + @")?? \] )";

			// (group names and comments are not supported by C++ Regex)

			string full_pattern = @"(?nsx)(" + Environment.NewLine +
				escape + " | " + Environment.NewLine +
				char_group + " | " + Environment.NewLine +
				".)";

			var regex = new Regex( full_pattern, RegexOptions.Compiled );

			foreach( Match m in regex.Matches( pattern ) )
			{
				Debug.Assert( m.Success );

				if( cnc.IsCancellationRequested ) return;

				// escapes and classes, '\...', [:...:]
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
			}
		}


		public Highlights HighlightPattern( ICancellable cnc, string pattern, int selectionStart, int selectionEnd, Segment visibleSegment )
		{
			Highlights highlights = new Highlights( );

			GrammarEnum grammar = OptionsControl.GetGrammar( );
			StringBuilder sb = new StringBuilder( @"(?nsx)(" );
			int para_size = 1;

			if( grammar == GrammarEnum.extended ||
				grammar == GrammarEnum.ECMAScript ||
				grammar == GrammarEnum.egrep ||
				grammar == GrammarEnum.awk )
			{
				sb.AppendLine( @"(?'left_para'\() |" );
				sb.AppendLine( @"(?'right_para'\)) |" );
			}

			if( grammar == GrammarEnum.basic ||
				grammar == GrammarEnum.grep )
			{
				sb.AppendLine( @"(?'left_para'\\\() |" );
				sb.AppendLine( @"(?'right_para'\\\)) |" );
				para_size = 2;
			}

			sb.AppendLine( @"(?'char_group'\[(\\.|.)*?(((?<!:)\])|$)) |" ); // (including incomplete classes)
			sb.AppendLine( @"(\\.)" );
			sb.AppendLine( @")" );

			string full_pattern = sb.ToString( );

			var regex = new Regex( full_pattern, RegexOptions.Compiled );

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

			var parentheses_at_left = parentheses.Where( g => ( g.Value == '(' && selectionStart > g.Index ) || ( g.Value == ')' && selectionStart > g.Index + ( para_size - 1 ) ) ).ToArray( );
			if( cnc.IsCancellationRequested ) return null;

			var parentheses_at_right = parentheses.Where( g => ( g.Value == '(' && selectionStart <= g.Index ) || ( g.Value == ')' && selectionStart <= g.Index + ( para_size - 1 ) ) ).ToArray( );
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
					var s = new Segment( g.Index, para_size );

					if( visibleSegment.Intersects( s ) ) highlights.LeftPara = s;
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
					var s = new Segment( g.Index, para_size );

					if( visibleSegment.Intersects( s ) ) highlights.RightPara = s;
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
