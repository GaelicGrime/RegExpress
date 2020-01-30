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


namespace Pcre2RegexEngineNs
{
	public class Pcre2RegexEngine : IRegexEngine
	{
		readonly UCPcre2RegexOptions OptionsControl;

		static readonly Dictionary<string, Regex> CachedColouringRegexes = new Dictionary<string, Regex>( );
		static readonly Dictionary<string, Regex> CachedHighlightingRegexes = new Dictionary<string, Regex>( );
		static readonly Regex EmptyRegex = new Regex( "(?!)" );


		public Pcre2RegexEngine( )
		{
			OptionsControl = new UCPcre2RegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;

		}


		#region IRegexEngine

		public string Id => "CppPcre2Regex";

		public string Name => "PCRE2";

		public string EngineVersion => Pcre2RegexInterop.Matcher.GetPcre2Version( );

		public RegexEngineCapabilityEnum Capabilities => RegexEngineCapabilityEnum.NoCaptures;

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

			return new Pcre2RegexInterop.Matcher( pattern, selected_options );
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

						continue;
					}
				}

				if( cnc.IsCancellationRequested ) return;

				// class (within [...] groups), '[:...:]', '[=...=]', '[. ... .]'
				{
					var g = m.Groups["class"];
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

				// named group, '(?<name>...)' or '(?'name'...)'
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
			int para_size = 1;

			var regex = GetCachedHighlightingRegex( );

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
							var s = new Segment( g.Index, para_size );

							if( visibleSegment.Intersects( s ) ) highlights.LeftCurlyBracket = s;

							if( normal_end )
							{
								var right = g.Index + g.Length - para_size;
								s = new Segment( right, para_size );

								if( visibleSegment.Intersects( s ) ) highlights.RightCurlyBracket = s;
							}
						}

						continue;
					}
				}
			}

			var parentheses_at_left = parentheses.Where( g => ( g.Value == '(' && selectionStart > g.Index ) || ( g.Value == ')' && selectionStart > g.Index + ( para_size - 1 ) ) ).ToArray( );
			if( cnc.IsCancellationRequested ) return;

			var parentheses_at_right = parentheses.Where( g => ( g.Value == '(' && selectionStart <= g.Index ) || ( g.Value == ')' && selectionStart <= g.Index + ( para_size - 1 ) ) ).ToArray( );
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
					var s = new Segment( g.Index, para_size );

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
					var s = new Segment( g.Index, para_size );

					if( visibleSegment.Intersects( s ) ) highlights.RightPara = s;
				}
			}
		}

		#endregion IRegexEngine


		private void OptionsControl_Changed( object sender, EventArgs e )
		{
			OptionsChanged?.Invoke( this, null );
		}


		Regex GetCachedColouringRegex( )
		{
			bool is_literal = OptionsControl.IsCompileOptionSelected( "PCRE2_LITERAL" );

			if( is_literal ) return EmptyRegex;

			bool is_extended = OptionsControl.IsCompileOptionSelected( "PCRE2_EXTENDED" );

			string key = string.Join( "\u001F", new object[] { is_extended } );

			lock( CachedColouringRegexes )
			{
				if( CachedColouringRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				string escape = @"(?'escape'";

				escape += @"\\c[A-Za-z] | "; // ASCII escape
				escape += @"\\0[0-7]{1,2} | "; // octal, two digits after 0
				escape += @"\\[0-7]{1,3} | "; // octal, three digits
				escape += @"\\o\{[0-9]+(\}|$) | "; // octal; bad values give error
				escape += @"\\N\{U\+[0-9a-fA-F]+(\}|$) | "; // hexa, error if no 'PCRE2_UTF'
				escape += @"\\x[0-9a-fA-F]{1,2} | "; // hexa, two digits
				escape += @"\\x\{[0-9a-fA-F]*(\}|$) | "; // hexa, error if empty
				escape += @"\\u[0-9a-fA-F]{1,4} | "; // hexa, four digits, error if no 'PCRE2_ALT_BSUX', 'PCRE2_EXTRA_ALT_BSUX'
				escape += @"\\u\{[0-9a-fA-F]*(\}|$) | "; // hexa, error if empty or no 'PCRE2_ALT_BSUX', 'PCRE2_EXTRA_ALT_BSUX'
				escape += @"\\[pP]\{.*?(\}|$) | "; // property

				// backreferences
				escape += @"\\[0-9]+ | "; // unbiguous
				escape += @"\\g[+]?[0-9]+ | ";
				escape += @"\\g\{[+]?[0-9]*(\}|$) | ";
				escape += @"\\[gk]<.*?(>|$) | ";
				escape += @"\\[gk]'.*?('|$) | ";
				escape += @"\\[gk]\{.*?(\}|$) | ";
				escape += @"\(\?P=.*?(\)|$) | "; //

				escape = Regex.Replace( escape, @"\s*\|\s*$", "" );
				escape += ")";

				// 

				string @class = @"(?'class'";

				@class += @"\[(?'c'[:=.]) .*? (\k<c>\] | $) | ";

				@class = Regex.Replace( @class, @"\s*\|\s*$", "" );
				@class += ")";

				//

				string char_group = @"(";

				char_group += @"\[ (" + @class + " | " + escape + " | . " + @")*? (\]|$) | ";

				char_group = Regex.Replace( char_group, @"\s*\|\s*$", "" );
				char_group += ")";

				// 

				string comment = @"(?'comment'";

				comment += @"\(\?\#.*?(\)|$) | "; // comment
				if( is_extended ) comment += @"\#.*?(\n|$) | "; // line-comment*/

				comment = Regex.Replace( comment, @"\s*\|\s*$", "" );
				comment += ")";

				//

				string named_group = @"(?'named_group'";

				named_group += @"\(\?(?'name'((?'a'')|<).*?(?(a)'|>)) | ";
				named_group += @"\(\?P(?'name'<.*?>) | ";

				named_group = Regex.Replace( named_group, @"\s*\|\s*$", "" );
				named_group += ")";

				// TODO: add support for '(*...)' constructs


				// 

				string pattern = @"(?nsx)(" + Environment.NewLine +
					escape + " | " + Environment.NewLine +
					comment + " | " + Environment.NewLine +
					char_group + " | " + Environment.NewLine +
					named_group + " | " + Environment.NewLine +
					"(.(?!)) )";

				regex = new Regex( pattern, RegexOptions.Compiled );

				CachedColouringRegexes.Add( key, regex );

				return regex;
			}
		}


		Regex GetCachedHighlightingRegex( )
		{
			bool is_literal = OptionsControl.IsCompileOptionSelected( "PCRE2_LITERAL" );

			if( is_literal ) return EmptyRegex;

			bool is_extended = OptionsControl.IsCompileOptionSelected( "PCRE2_EXTENDED" );

			string key = string.Join( "\u001F", new object[] { is_extended } );

			lock( CachedHighlightingRegexes )
			{
				if( CachedHighlightingRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				string pattern = @"(?nsx)(";

				pattern += @"(?'left_para'\() | "; // '('
				pattern += @"(?'right_para'\)) | "; // ')'
				pattern += @"(?'range'\{(\\.|.)*?(\}(?'end')|$)) | "; // '{...}'

				pattern += @"(?'char_group'\[ ((\[:.*? (:\]|$)) | \\. | .)*? (\](?'end')|$) ) | "; // (including incomplete classes)
				pattern += @"\\. | . | ";

				pattern = Regex.Replace( pattern, @"\s*\|\s*$", "" );
				pattern += @")";

				regex = new Regex( pattern, RegexOptions.Compiled );

				CachedHighlightingRegexes.Add( key, regex );

				return regex;
			}
		}
	}
}
