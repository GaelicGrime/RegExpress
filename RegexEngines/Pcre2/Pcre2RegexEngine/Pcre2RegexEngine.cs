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

		public string NoteForCaptures => null;

		public event RegexEngineOptionsChanged OptionsChanged;


		public Control GetOptionsControl( )
		{
			return OptionsControl;
		}


		public string[] ExportOptions( )
		{
			return OptionsControl.ExportOptions( );
		}


		public void ImportOptions( string[] options )
		{
			OptionsControl.ImportOptions( options );
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
			int par_size = 1;
			int bracket_size = 1;

			Regex regex = GetCachedHighlightingRegex( );

			HighlightHelper.CommonHighlighting( cnc, highlights, pattern, selectionStart, selectionEnd, visibleSegment, regex, par_size, bracket_size );
		}

		#endregion IRegexEngine


		private void OptionsControl_Changed( object sender, EventArgs e )
		{
			OptionsChanged?.Invoke( this );
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

				regex = CreateColouringRegex( );

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

				regex = CreateHighlightingRegex( );

				CachedHighlightingRegexes.Add( key, regex );

				return regex;
			}
		}


		Regex CreateColouringRegex( )
		{
			bool is_literal = OptionsControl.IsCompileOptionSelected( "PCRE2_LITERAL" );

			if( is_literal ) return EmptyRegex;

			bool is_extended = OptionsControl.IsCompileOptionSelected( "PCRE2_EXTENDED" );

			string escape = "";

			escape += @"\\c[A-Za-z] | "; // ASCII escape
			escape += @"\\0[0-7]{1,2} | "; // octal, two digits after 0
			escape += @"\\[0-7]{1,3} | "; // octal, three digits
			escape += @"\\o\{[0-9]+(\} | $) | "; // octal; bad values give error
			escape += @"\\N\{U\+[0-9a-fA-F]+(\} | $) | "; // hexa, error if no 'PCRE2_UTF'
			escape += @"\\x[0-9a-fA-F]{1,2} | "; // hexa, two digits
			escape += @"\\x\{[0-9a-fA-F]*(\} | $) | "; // hexa, error if empty
			escape += @"\\u[0-9a-fA-F]{1,4} | "; // hexa, four digits, error if no 'PCRE2_ALT_BSUX', 'PCRE2_EXTRA_ALT_BSUX'
			escape += @"\\u\{[0-9a-fA-F]*(\} | $) | "; // hexa, error if empty or no 'PCRE2_ALT_BSUX', 'PCRE2_EXTRA_ALT_BSUX'
			escape += @"\\[pP]\{.*?(\} | $) | "; // property
			escape += @"\\Q.*?(\\E|$) | "; // quoted sequence, \Q...\E

			// backreferences
			escape += @"\\[0-9]+ | "; // unbiguous
									  // see also named groups

			escape += @"\\. | ";

			escape = EndGroup( escape, "escape" );

			// 

			string @class = "";

			@class += @"\[(?'c'[:=.]) .*? (\k<c>\] | $) | ";

			@class = EndGroup( @class, "class" );

			//

			string char_group = "";

			char_group += @"\[ (" + @class + " | " + escape + " | . " + @")*? (\]|$) | ";

			char_group = EndGroup( char_group, null );

			// 

			string comment = "";

			comment += @"\(\?\#.*?(\)|$) | "; // comment
			if( is_extended ) comment += @"\#.*?(\n|$) | "; // line-comment*/

			comment = EndGroup( comment, "comment" );

			//

			string named_group = "";

			named_group += @"\(\?(?'name'((?'a'')|<).*?(?(a)'|>)) | ";
			named_group += @"\(\?P(?'name'<.*?>) | ";
			named_group += @"(?'name'\\g[+]?[0-9]+) | ";
			named_group += @"(?'name'\\g\{[+]?[0-9]*(\} | $)) | ";
			named_group += @"(?'name'\\[gk]<.*?(>|$)) | ";
			named_group += @"(?'name'\\[gk]'.*?('|$)) | ";
			named_group += @"(?'name'\\[gk]\{.*?(\}|$)) | ";
			named_group += @"(?'name'\(\?P=.*?(\)|$)) | "; //

			named_group = EndGroup( named_group, "named_group" );

			// TODO: add support for '(*...)' constructs


			// 

			string[] all = new[]
			{
				comment,
				named_group,
				escape,
				char_group,
			};

			string pattern = @"(?nsx)(" + Environment.NewLine +
				string.Join( " | " + Environment.NewLine, all.Where( s => !string.IsNullOrWhiteSpace( s ) ) ) +
				")";

			var regex = new Regex( pattern, RegexOptions.Compiled );

			return regex;
		}


		Regex CreateHighlightingRegex( )
		{
			bool is_literal = OptionsControl.IsCompileOptionSelected( "PCRE2_LITERAL" );

			if( is_literal ) return EmptyRegex;

			string pattern = @"";

			pattern += @"(?'left_par'\() | "; // '('
			pattern += @"(?'right_par'\)) | "; // ')'
			pattern += @"(?'left_brace'\{).*?((?'right_brace'\})|$) | "; // '{...}'
			pattern += @"((?'left_bracket'\[) ((\[:.*? (:\]|$)) | \\. | .)*? ((?'right_bracket'\])|$) ) | "; // [...]
			pattern += @"\\."; // '\...'

			pattern = EndGroup( pattern, null );

			if( string.IsNullOrWhiteSpace( pattern ) )
				pattern = "(?!)";
			else
				pattern = "(?nsx)" + pattern;

			var regex = new Regex( pattern, RegexOptions.Compiled );

			return regex;
		}


		static readonly Regex EndGroupRegex = new Regex( @"(\s*\|\s*)?$", RegexOptions.ExplicitCapture | RegexOptions.Compiled );

		static string EndGroup( string s, string name )
		{
			if( string.IsNullOrWhiteSpace( s ) ) return null;

			if( name != null )
			{
				s = "(?'" + name + "'" + EndGroupRegex.Replace( s, ")", 1 );
			}
			else
			{
				s = "(" + EndGroupRegex.Replace( s, ")", 1 );
			}

			return s;
		}
	}
}
