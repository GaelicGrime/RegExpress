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


namespace OnigurumaRegexEngineNs
{
	public class OnigurumaRegexEngine : IRegexEngine
	{
		readonly UCOnigurumaRegexOptions OptionsControl;

		static readonly Dictionary<string, Regex> CachedColouringRegexes = new Dictionary<string, Regex>( );
		static readonly Dictionary<string, Regex> CachedHighlightingRegexes = new Dictionary<string, Regex>( );


		public OnigurumaRegexEngine( )
		{
			OptionsControl = new UCOnigurumaRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "OnigurumaRegex";

		public string Name => "Oniguruma";

		public string EngineVersion => OnigurumaRegexInterop.Matcher.GetVersion( );

		public RegexEngineCapabilityEnum Capabilities => RegexEngineCapabilityEnum.Default;

		public string NoteForCaptures => "requires ‘ONIG_SYN_OP2_ATMARK_CAPTURE_HISTORY’";


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

			return new OnigurumaRegexInterop.Matcher( pattern, selected_options );
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

				//......
#if false
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

#endif
				if( cnc.IsCancellationRequested ) return;

				// named groups and back references
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
			var helper = OptionsControl.CreateOnigurumaHelper( );

			int par_size = helper.IsONIG_SYN_OP_ESC_LPAREN_SUBEXP ? 2 : 1;
			int bracket_size = 1;

			Regex regex = GetCachedHighlightingRegex( helper );

			HighlightHelper.CommonHighlighting( cnc, highlights, pattern, selectionStart, selectionEnd, visibleSegment, regex, par_size, bracket_size );
		}

		#endregion


		private void OptionsControl_Changed( object sender, RegexEngineOptionsChangedArgs args )
		{
			OptionsChanged?.Invoke( this, args );
		}


		Regex GetCachedColouringRegex( )
		{
			var helper = OptionsControl.CreateOnigurumaHelper( );
			string key = helper.GetKey( );

			lock( CachedColouringRegexes )
			{
				if( CachedColouringRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateColouringRegex( helper );

				CachedColouringRegexes.Add( key, regex );

				return regex;
			}
		}


		Regex GetCachedHighlightingRegex( OnigurumaRegexInterop.OnigurumaHelper helper )
		{
			string key = helper.GetKey( );

			lock( CachedHighlightingRegexes )
			{
				if( CachedHighlightingRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateHighlightingRegex( helper );

				CachedHighlightingRegexes.Add( key, regex );

				return regex;
			}
		}


		Regex CreateColouringRegex( OnigurumaRegexInterop.OnigurumaHelper helper )
		{
			string normal = "";

			if( helper.IsONIG_SYN_OP_ESC_LPAREN_SUBEXP )
			{
				normal += @"\\\( | \\\) | ";
			}

			if( helper.IsONIG_SYN_OP_ESC_BRACE_INTERVAL )
			{
				normal += @"\\\{ | \\\} | ";
			}

			normal = RegexUtilities.EndGroup( normal, null );

			//

			string escape = "";

			if( !helper.IsONIG_SYNTAX_ASIS )
			{
				escape += @"\\0[0-7]{1,2} | "; // octal, two digits after 0
				escape += @"\\[0-7]{1,3} | "; // octal, three digits

				if( helper.IsONIG_SYN_OP_ESC_O_BRACE_OCTAL ) escape += @"\\o\{[0-7]+ (\s+ [0-7]+)* (\}|$) | "; // \o{17777777777 ...} wide octal chars

				escape += @"\\u[0-9a-fA-F]+ | "; // \uHHHH wide hexadecimal char
				if( helper.IsONIG_SYN_OP_ESC_X_HEX2 ) escape += @"\\x[0-9a-fA-F]+ | "; // \xHH hexadecimal char 
				if( helper.IsONIG_SYN_OP_ESC_X_BRACE_HEX8 ) escape += @"\\x\{[0-9a-fA-F]+ (\s+ [0-9a-fA-F]+)* (\}|$) | "; // \x{7HHHHHHH ...} wide hexadecimal chars

				if( helper.IsONIG_SYN_OP_ESC_C_CONTROL )
				{
					escape += @"\\c[A-Za-z] | "; // \cx control char
					escape += @"\\C-([A-Za-z])? | "; // \C-x control char
				}

				escape += @"\\M-([A-Za-z])? | "; // \M-x meta  (x|0x80)
				escape += @"\\M-(\\C-([A-Za-z])?)? | "; // \M-x meta control char

				escape += @"\\[pP]\{.*?(\} | $) | "; // property

				/*
				Probably not useful

				if( helper.IsONIG_SYN_OP_ESC_ASTERISK_ZERO_INF )
				{
					escape += @"(?!\\\*)";
				}

				if( helper.IsONIG_SYN_OP_ESC_PLUS_ONE_INF )
				{
					escape += @"(?!\\\+)";
				}

				if( helper.IsONIG_SYN_OP_ESC_QMARK_ZERO_ONE )
				{
					escape += @"(?!\\\?)";
				}

				if( helper.IsONIG_SYN_OP_ESC_BRACE_INTERVAL )
				{
					escape += @"(?!\\[{}])";
				}
				*/

				escape += @"\\. | ";
			}

			escape = RegexUtilities.EndGroup( escape, "escape" );

			//

			string quote = "";

			if( !helper.IsONIG_SYNTAX_ASIS && helper.IsONIG_SYN_OP2_ESC_CAPITAL_Q_QUOTE )
			{

				quote = @"\\Q.*?(\\E|$) | "; // quoted part
			}

			quote = RegexUtilities.EndGroup( quote, "escape" ); // use 'escape' name to take its colour

			// (nested groups: https://stackoverflow.com/questions/546433/regular-expression-to-match-balanced-parentheses)

			string char_group = "";
			string posix_bracket = "";
			if( helper.IsONIG_SYN_OP_POSIX_BRACKET ) posix_bracket = @"(?'escape'\[:.*?(:\]|$)) |"; // [:...:], use escape colour

			if( !helper.IsONIG_SYNTAX_ASIS )
			{
				char_group = $@"
						\[ 
						\]?
						(?> {posix_bracket} \[(?<c>) | ({escape} | [^\[\]])+ | \](?<-c>))*
						(?(c)(?!))
						\]
						";
			}

			char_group = RegexUtilities.EndGroup( char_group, null );

			//

			string comment = "";

			if( !helper.IsONIG_SYNTAX_ASIS )
			{
				if( helper.IsONIG_SYN_OP2_QMARK_GROUP_EFFECT ) comment += @"\(\?\#.*?(\)|$) | "; // comment
			}

			if( helper.IsONIG_OPTION_EXTEND ) comment += @"\#.*?(\n|$) | "; // line-comment

			comment = RegexUtilities.EndGroup( comment, "comment" );

			//

			string named_group = "";

			if( helper.IsONIG_SYN_OP2_QMARK_LT_NAMED_GROUP )
			{
				named_group += @"\(\?(?'name'<(?![=!]).*?(>|$)) | ";
				named_group += @"\(\?(?'name''.*?('|$)) | ";
			}
			if( helper.IsONIG_SYN_OP2_ATMARK_CAPTURE_HISTORY )
			{
				named_group += @"\(\?@(?'name'<.*?(>|$)) | ";
				named_group += @"\(\?@(?'name''.*?('|$)) | ";
			}
			if( helper.IsONIG_SYN_OP2_ESC_K_NAMED_BACKREF )
			{
				named_group += @"(?'name'\\k<.*?(>|$)) | ";
				named_group += @"(?'name'\\k'.*?('|$)) | ";
			}
			if( helper.IsONIG_SYN_OP2_ESC_G_SUBEXP_CALL )
			{
				named_group += @"(?'name'\\g<.*?(>|$)) | ";
				named_group += @"(?'name'\\g'.*?('|$)) | ";
			}

			named_group = RegexUtilities.EndGroup( named_group, "named_group" );

			//

			string[] all = new[]
			{
				// (order is important)
				comment,
				quote,
				named_group,
				char_group,
				normal,
				escape,
			};

			string pattern = @"(?nsx)(" + Environment.NewLine +
				string.Join( " | " + Environment.NewLine, all.Where( s => !string.IsNullOrWhiteSpace( s ) ) ) +
				")";

			var regex = new Regex( pattern, RegexOptions.Compiled );

			return regex;
		}


		Regex CreateHighlightingRegex( OnigurumaRegexInterop.OnigurumaHelper helper )
		{
			string pattern = "";

			if( !helper.IsONIG_SYNTAX_ASIS )
			{
				if( helper.IsONIG_SYN_OP2_QMARK_GROUP_EFFECT ) pattern += @"\(\?\#.*?(\)|$) | "; // comment

				if( helper.IsONIG_OPTION_EXTEND ) pattern += @"\#.*?(\n|$) | "; // line-comment
				if( helper.IsONIG_SYN_OP2_ESC_CAPITAL_Q_QUOTE ) pattern += @"\\Q.*?(\\E|$) | "; // quoted part

				if( helper.IsONIG_SYN_OP_LPAREN_SUBEXP )
				{
					pattern += @"(?'left_par'\() | "; // '('
					pattern += @"(?'right_par'\)) | "; // ')'
				}

				if( helper.IsONIG_SYN_OP_ESC_LPAREN_SUBEXP )
				{
					pattern += @"(?'left_par'\\\() | "; // '\('
					pattern += @"(?'right_par'\\\)) | "; // '\)'
				}

				if( helper.IsONIG_SYN_OP_ESC_O_BRACE_OCTAL ) pattern += @"\\o\{[0-7]+ (\s+ [0-7]+)* (\}|$) | "; // \o{17777777777 ...} wide octal chars
				if( helper.IsONIG_SYN_OP_ESC_X_BRACE_HEX8 ) pattern += @"\\x\{[0-9a-fA-F]+ (\s+ [0-9a-fA-F]+)* (\}|$) | "; // \x{7HHHHHHH ...} wide hexadecimal chars

				if( helper.IsONIG_SYN_OP2_ESC_P_BRACE_CHAR_PROPERTY || helper.IsONIG_SYN_OP2_ESC_P_BRACE_CIRCUMFLEX_NOT )
				{
					pattern += @"\\[pP]\{.*?(\} | $) | "; // property
				}

				if( helper.IsONIG_SYN_OP_BRACE_INTERVAL ) pattern += @"(?'left_brace'\{).*?((?'right_brace'\})|$) | "; // '{...}'
				if( helper.IsONIG_SYN_OP_ESC_BRACE_INTERVAL ) pattern += @"(?'left_brace'\\{).*?((?'right_brace'\\})|$) | "; // '\{...\}'

				string posix_bracket = "";
				if( helper.IsONIG_SYN_OP_POSIX_BRACKET ) posix_bracket = @"(\[:.*?(:\]|$)) |"; // [:...:]

				if( helper.IsONIG_SYN_OP_BRACKET_CC )
				{
					pattern += $@"
						(?'left_bracket'\[)
						\]?
						(?> {posix_bracket} (?'left_bracket'\[)(?<c>) | (\\. | [^\[\]])+ | (?'right_bracket'\])(?<-c>))*
						(?(c)(?!))
						(?'right_bracket'\])?
						|
						(?'right_bracket'\])
						| ";
				}

				pattern += @"\\. | "; // '\...'
			}

			pattern = RegexUtilities.EndGroup( pattern, null );

			if( string.IsNullOrWhiteSpace( pattern ) )
				pattern = "(?!)";
			else
				pattern = "(?nsx)" + pattern;

			var regex = new Regex( pattern, RegexOptions.Compiled );

			return regex;
		}

	}
}