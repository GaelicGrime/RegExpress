using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.SyntaxColouring;
using RustRegexEngineNs.Matches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace RustRegexEngineNs
{
	public class RustRegexEngine : IRegexEngine
	{
		readonly UCRustRegexOptions OptionsControl;
		static readonly object RustVersionLocker = new object( );
		static string RustVersion = null;


		public RustRegexEngine( )
		{
			OptionsControl = new UCRustRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "RustRegex";

		public string Name => "Rust Regex";

		public string EngineVersion
		{
			get
			{
				if( RustVersion == null )
				{
					lock( RustVersionLocker )
					{
						if( RustVersion == null )
						{
							try
							{
								RustVersion = RustMatcher.GetRustVersion( NonCancellable.Instance );
							}
							catch
							{
								RustVersion = "Unknown Version";
							}
						}
					}
				}

				return RustVersion;
			}
		}

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

			return new RustMatcher( pattern, selected_options );
		}


		public void ColourisePattern( ICancellable cnc, ColouredSegments colouredSegments, string pattern, Segment visibleSegment )
		{

		}


		public void HighlightPattern( ICancellable cnc, Highlights highlights, string pattern, int selectionStart, int selectionEnd, Segment visibleSegment )
		{
			//...
		}

		#endregion IRegexEngine


		private void OptionsControl_Changed( object sender, RegexEngineOptionsChangedArgs args )
		{
			OptionsChanged?.Invoke( this, args );
		}


#if false
		Regex CreateColouringRegex( )
		{
			var pb_escape = new PatternBuilder( );

			pb_escape.BeginGroup( "escape" );

			pb_escape.Add( @"\\x[0-9a-fA-F]{1,2}" ); // \x7F hexadecimal char 
			pb_escape.Add( @"\\x\{[0-9a-fA-F]+(\}|$)" ); // \x{10FFFF} wide hexadecimal chars







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

			if( helper.IsONIG_SYN_OP2_ESC_CAPITAL_Q_QUOTE )
			{
				pb_escape.Add( @"\\Q.*?(\\E|$)" ); // quoted part; use 'escape' name to take its colour
			}

			pb_escape.EndGroup( );

			var pb = new PatternBuilder( );

			pb.BeginGroup( "comment" );

			if( helper.IsONIG_SYN_OP2_QMARK_GROUP_EFFECT ) pb.Add( @"\(\?\#.*?(\)|$)" ); // comment
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

			pb.Add( $@"
						\[ 
						\]?
						(?> {posix_bracket}{( posix_bracket.Length == 0 ? "" : " |" )} \[(?<c>) | ({pb_escape.ToPattern( )} | [^\[\]])+ | \](?<-c>))*
						(?(c)(?!))
						\]
						" );

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
#endif

	}
}
