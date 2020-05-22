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
			var pb_escape = new PatternBuilder( );

			pb_escape.BeginGroup( "escape" );

			if( !helper.IsONIG_SYNTAX_ASIS )
			{
				pb_escape.Add( @"\\0[0-7]{1,2}" ); // octal, two digits after 0
				pb_escape.Add( @"\\[0-7]{1,3}" ); // octal, three digits

				if( helper.IsONIG_SYN_OP_ESC_O_BRACE_OCTAL ) pb_escape.Add( @"\\o\{[0-7]+ (\s+ [0-7]+)* (\}|$)" ); // \o{17777777777 ...} wide octal chars

				pb_escape.Add( @"\\u[0-9a-fA-F]+" ); // \uHHHH wide hexadecimal char
				if( helper.IsONIG_SYN_OP_ESC_X_HEX2 ) pb_escape.Add( @"\\x[0-9a-fA-F]+" ); // \xHH hexadecimal char 
				if( helper.IsONIG_SYN_OP_ESC_X_BRACE_HEX8 ) pb_escape.Add( @"\\x\{[0-9a-fA-F]+ (\s+ [0-9a-fA-F]+)* (\}|$)" ); // \x{7HHHHHHH ...} wide hexadecimal chars

				if( helper.IsONIG_SYN_OP_ESC_C_CONTROL )
				{
					pb_escape.Add( @"\\c[A-Za-z]" ); // \cx control char
					pb_escape.Add( @"\\C-([A-Za-z])?" ); // \C-x control char
				}

				pb_escape.Add( @"\\M-([A-Za-z])?" ); // \M-x meta  (x|0x80)
				pb_escape.Add( @"\\M-(\\C-([A-Za-z])?)?" ); // \M-x meta control char
				pb_escape.Add( @"\\[pP]\{.*?(\} | $)" ); // property

				/*
				Probably not useful

				if( helper.IsONIG_SYN_OP_ESC_ASTERISK_ZERO_INF )
				{
					pb_escape.Add( @"(?!\\\*)");
				}

				if( helper.IsONIG_SYN_OP_ESC_PLUS_ONE_INF )
				{
					pb_escape.Add( @"(?!\\\+)");
				}

				if( helper.IsONIG_SYN_OP_ESC_QMARK_ZERO_ONE )
				{
					pb_escape.Add( @"(?!\\\?)");
				}

				if( helper.IsONIG_SYN_OP_ESC_BRACE_INTERVAL )
				{
					pb_escape.Add( @"(?!\\[{}])");
				}
				*/

				pb_escape.Add( @"\\." );
			}

			if( !helper.IsONIG_SYNTAX_ASIS && helper.IsONIG_SYN_OP2_ESC_CAPITAL_Q_QUOTE )
			{
				pb_escape.Add( @"\\Q.*?(\\E|$)" ); // quoted part; use 'escape' name to take its colour
			}

			pb_escape.EndGroup( );

			var pb = new PatternBuilder( );

			pb.BeginGroup( "comment" );
			if( !helper.IsONIG_SYNTAX_ASIS )
			{
				if( helper.IsONIG_SYN_OP2_QMARK_GROUP_EFFECT ) pb.Add( @"\(\?\#.*?(\)|$)" ); // comment
			}
			if( helper.IsONIG_OPTION_EXTEND ) pb.Add( @"\#.*?(\n|$)" ); // line-comment
			pb.EndGroup( );

			if( helper.IsONIG_SYN_OP2_QMARK_LT_NAMED_GROUP )
			{
				pb.Add( @"\(\?(?'name'<(?![=!]).*?(>|$))" );
				pb.Add( @"\(\?(?'name''.*?('|$))" );
			}
			if( helper.IsONIG_SYN_OP2_ATMARK_CAPTURE_HISTORY )
			{
				pb.Add( @"\(\?@(?'name'<.*?(>|$))" );
				pb.Add( @"\(\?@(?'name''.*?('|$))" );
			}
			if( helper.IsONIG_SYN_OP2_ESC_K_NAMED_BACKREF )
			{
				pb.Add( @"(?'name'\\k<.*?(>|$))" );
				pb.Add( @"(?'name'\\k'.*?('|$))" ); ;
			}
			if( helper.IsONIG_SYN_OP2_ESC_G_SUBEXP_CALL )
			{
				pb.Add( @"(?'name'\\g<.*?(>|$))" );
				pb.Add( @"(?'name'\\g'.*?('|$))" );
			}

			// (nested groups: https://stackoverflow.com/questions/546433/regular-expression-to-match-balanced-parentheses)

			string posix_bracket = "";
			if( helper.IsONIG_SYN_OP_POSIX_BRACKET ) posix_bracket = @"(?'escape'\[:.*?(:\]|$))"; // [:...:], use escape colour

			if( !helper.IsONIG_SYNTAX_ASIS )
			{
				pb.Add( $@"
						\[ 
						\]?
						(?> {posix_bracket}{( posix_bracket.Length == 0 ? "" : " |" )} \[(?<c>) | ({pb_escape.ToPattern( )} | [^\[\]])+ | \](?<-c>))*
						(?(c)(?!))
						\]
						" );
			}

			if( helper.IsONIG_SYN_OP_ESC_LPAREN_SUBEXP )
			{
				pb.Add( @"\\\( | \\\)" ); // (skip)
			}

			if( helper.IsONIG_SYN_OP_ESC_BRACE_INTERVAL )
			{
				pb.Add( @"\\\{ | \\\}" ); // (skip)
			}

			pb.Add( pb_escape.ToPattern( ) );

			return pb.ToRegex( );
		}


		Regex CreateHighlightingRegex( OnigurumaRegexInterop.OnigurumaHelper helper )
		{
			var pb = new PatternBuilder( );

			if( !helper.IsONIG_SYNTAX_ASIS )
			{
				if( helper.IsONIG_SYN_OP2_QMARK_GROUP_EFFECT ) pb.Add( @"\(\?\#.*?(\)|$)" ); // comment

				if( helper.IsONIG_OPTION_EXTEND ) pb.Add( @"\#.*?(\n|$)" ); // line-comment
				if( helper.IsONIG_SYN_OP2_ESC_CAPITAL_Q_QUOTE ) pb.Add( @"\\Q.*?(\\E|$)" ); // quoted part

				if( helper.IsONIG_SYN_OP_LPAREN_SUBEXP )
				{
					pb.Add( @"(?'left_par'\()" ); // '('
					pb.Add( @"(?'right_par'\))" ); // ')'
				}

				if( helper.IsONIG_SYN_OP_ESC_LPAREN_SUBEXP )
				{
					pb.Add( @"(?'left_par'\\\()" ); // '\('
					pb.Add( @"(?'right_par'\\\))" ); // '\)'
				}

				if( helper.IsONIG_SYN_OP_ESC_O_BRACE_OCTAL ) pb.Add( @"\\o\{.*?(\}|$)" ); // \o{17777777777 ...} wide octal chars
				if( helper.IsONIG_SYN_OP_ESC_X_BRACE_HEX8 ) pb.Add( @"\\x\{.*?(\}|$)" ); // \x{7HHHHHHH ...} wide hexadecimal chars

				if( helper.IsONIG_SYN_OP2_ESC_P_BRACE_CHAR_PROPERTY || helper.IsONIG_SYN_OP2_ESC_P_BRACE_CIRCUMFLEX_NOT )
				{
					pb.Add( @"\\[pP]\{.*?(\} | $)" ); // property
				}

				if( helper.IsONIG_SYN_OP_BRACE_INTERVAL ) pb.Add( @"(?'left_brace'\{) (\d+(,\d*)? | ,\d+) ((?'right_brace'\})|$)" ); // '{...}'
				if( helper.IsONIG_SYN_OP_ESC_BRACE_INTERVAL ) pb.Add( @"(?'left_brace'\\{).*?((?'right_brace'\\})|$)" ); // '\{...\}'

				string posix_bracket = "";
				if( helper.IsONIG_SYN_OP_POSIX_BRACKET ) posix_bracket = @"(\[:.*?(:\]|$))"; // [:...:]

				if( helper.IsONIG_SYN_OP_BRACKET_CC )
				{
					pb.Add( $@"
						(?'left_bracket'\[)
						\]?
						(?> {posix_bracket}{( posix_bracket.Length == 0 ? "" : " |" )} (?'left_bracket'\[)(?<c>) | (\\. | [^\[\]])+ | (?'right_bracket'\])(?<-c>))*
						(?(c)(?!))
						(?'right_bracket'\])?
						|
						(?'right_bracket'\])
						" );
				}

				pb.Add( @"\\." ); // '\...'
			}

			return pb.ToRegex( );
		}

	}
}
