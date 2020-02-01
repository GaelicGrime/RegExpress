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


namespace BoostRegexEngineNs
{
	public class BoostRegexEngine : IRegexEngine
	{
		readonly UCBoostRegexOptions OptionsControl;

		struct Key
		{
			internal GrammarEnum Grammar;
			internal bool ModX;
		}

		static readonly Dictionary<Key, Regex> CachedColouringRegexes = new Dictionary<Key, Regex>( );
		static readonly Dictionary<Key, Regex> CachedHighlightingRegexes = new Dictionary<Key, Regex>( );


		public BoostRegexEngine( )
		{
			OptionsControl = new UCBoostRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "CppBoostRegex";

		public string Name => "Boost.Regex";

		public string EngineVersion => BoostRegexInterop.Matcher.GetBoostVersion( );

		public RegexEngineCapabilityEnum Capabilities => RegexEngineCapabilityEnum.Default;


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
			var selected_options = OptionsControl.CachedOptions;

			return new BoostRegexInterop.Matcher( pattern, selected_options );
		}


		public void ColourisePattern( ICancellable cnc, ColouredSegments colouredSegments, string pattern, Segment visibleSegment )
		{
			GrammarEnum grammar = OptionsControl.GetGrammar( );
			bool mod_x = OptionsControl.GetModX( );

			Regex regex = GetCachedColouringRegex( grammar, mod_x );

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
			GrammarEnum grammar = OptionsControl.GetGrammar( );
			bool mod_x = OptionsControl.GetModX( );

			int para_size = 1;

			bool is_POSIX_basic =
				grammar == GrammarEnum.basic ||
				grammar == GrammarEnum.sed ||
				grammar == GrammarEnum.grep ||
				grammar == GrammarEnum.emacs;

			if( is_POSIX_basic )
			{
				para_size = 2;
			}

			var regex = GetCachedHighlightingRegex( grammar, mod_x );

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


		static Regex GetCachedColouringRegex( GrammarEnum grammar, bool modX )
		{
			var key = new Key { Grammar = grammar, ModX = modX };

			lock( CachedColouringRegexes )
			{
				if( CachedColouringRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				bool is_perl =
					grammar == GrammarEnum.perl ||
					grammar == GrammarEnum.ECMAScript ||
					grammar == GrammarEnum.normal ||
					grammar == GrammarEnum.JavaScript ||
					grammar == GrammarEnum.JScript;

				bool is_POSIX_extended =
					grammar == GrammarEnum.extended ||
					grammar == GrammarEnum.egrep ||
					grammar == GrammarEnum.awk;

				bool is_POSIX_basic =
					grammar == GrammarEnum.basic ||
					grammar == GrammarEnum.sed ||
					grammar == GrammarEnum.grep ||
					grammar == GrammarEnum.emacs;

				bool is_emacs =
					grammar == GrammarEnum.emacs;

				string escape = @"(?'escape'";

				if( is_perl || is_POSIX_extended || is_POSIX_basic ) escape += @"\\[1-9] | "; // back reference
				if( is_perl ) escape += @"\\g-?[1-9] | \\g\{.*?\} | "; // back reference
				if( is_perl ) escape += @"\\k<.*?(>|$) | "; // back reference

				if( is_perl || is_POSIX_extended ) escape += @"\\c[A-Za-z] | "; // ASCII escape
				if( is_perl || is_POSIX_extended ) escape += @"\\x[0-9A-Fa-f]{1,2} | "; // hex, two digits
				if( is_perl || is_POSIX_extended ) escape += @"\\x\{[0-9A-Fa-f]+(\}|$) | "; // hex, four digits
				if( is_perl || is_POSIX_extended ) escape += @"\\0[0-7]{1,3} | "; // octal, three digits
				if( is_perl || is_POSIX_extended ) escape += @"\\N\{.*?(\}|$) | "; // symbolic name
				if( is_perl || is_POSIX_extended ) escape += @"\\[pP]\{.*?(\}|$) | "; // property
				if( is_perl || is_POSIX_extended ) escape += @"\\[pP]. | "; // property, short name
				if( is_perl || is_POSIX_extended ) escape += @"\\Q.*?(\\E|$) | "; // quoted sequence
				if( is_emacs ) escape += @"\\[sS]. | "; // syntax group

				if( is_perl || is_POSIX_extended ) escape += @"\\. | "; // various
				if( is_POSIX_basic ) escape += @"(?!\\\( | \\\) | \\\{ | \\\})\\. | "; // various

				escape = Regex.Replace( escape, @"\s*\|\s*$", "" );
				escape += ")";

				// 

				string comment = @"(?'comment'";

				if( is_perl ) comment += @"\(\?\#.*?(\)|$) | "; // comment
				if( is_perl && modX ) comment += @"\#.*?(\n|$) | "; // line-comment*/

				comment = Regex.Replace( comment, @"\s*\|\s*$", "" );
				comment += ")";

				// 

				string @class = @"(?'class'";

				if( is_perl || is_POSIX_extended || is_POSIX_basic ) @class += @"\[(?'c'[:=.]) .*? (\k<c>\] | $) | ";

				@class = Regex.Replace( @class, @"\s*\|\s*$", "" );
				@class += ")";

				//

				string char_group = @"(";

				if( is_perl || is_POSIX_basic ) char_group += @"\[ (" + @class + " | " + escape + " | . " + @")*? (\]|$) | ";

				char_group = Regex.Replace( char_group, @"\s*\|\s*$", "" );
				char_group += ")";

				//

				string named_group = @"(?'named_group'";

				if( is_perl ) named_group += @"\(\?(?'name'((?'a'')|<).*?(?(a)'|>))";

				named_group = Regex.Replace( named_group, @"\s*\|\s*$", "" );
				named_group += ")";


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


		static Regex GetCachedHighlightingRegex( GrammarEnum grammar, bool modX )
		{
			var key = new Key { Grammar = grammar, ModX = modX };

			lock( CachedHighlightingRegexes )
			{
				if( CachedHighlightingRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				bool is_perl =
					grammar == GrammarEnum.perl ||
					grammar == GrammarEnum.ECMAScript ||
					grammar == GrammarEnum.normal ||
					grammar == GrammarEnum.JavaScript ||
					grammar == GrammarEnum.JScript;

				bool is_POSIX_extended =
					grammar == GrammarEnum.extended ||
					grammar == GrammarEnum.egrep ||
					grammar == GrammarEnum.awk;

				bool is_POSIX_basic =
					grammar == GrammarEnum.basic ||
					grammar == GrammarEnum.sed ||
					grammar == GrammarEnum.grep ||
					grammar == GrammarEnum.emacs;

				bool is_emacs =
					grammar == GrammarEnum.emacs;

				string pattern = @"(?nsx)(";

				if( is_perl || is_POSIX_extended )
				{
					pattern += @"(?'left_para'\() | "; // '('
					pattern += @"(?'right_para'\)) | "; // ')'
					pattern += @"(?'range'\{\s*\d+(\s*,(\s*\d+)?)?(\s*\}(?'end')|$)) | "; // '{...}' (spaces are allowed)
				}

				if( is_POSIX_basic )
				{
					pattern += @"(?'left_para'\\\() | "; // '\('
					pattern += @"(?'right_para'\\\)) | "; // '\)'
					pattern += @"(?'range'\\{.*?(\\}(?'end')|$)) | "; // '\{...\}'
				}

				if( is_perl || is_POSIX_extended || is_POSIX_basic )
				{
					pattern += @"(?'char_group'\[ ((\[:.*? (:\]|$)) | \\. | .)*? (\](?'end')|$) ) | "; // (including incomplete classes)
					pattern += @"\\. | . | ";
				}

				pattern = Regex.Replace( pattern, @"\s*\|\s*$", "" );
				pattern += @")";

				regex = new Regex( pattern, RegexOptions.Compiled );

				CachedHighlightingRegexes.Add( key, regex );

				return regex;
			}
		}
	}
}
