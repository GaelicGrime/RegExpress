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
		readonly UCOnigurimaRegexOptions OptionsControl;

		static readonly Dictionary<string, Regex> CachedColouringRegexes = new Dictionary<string, Regex>( );
		static readonly Dictionary<string, Regex> CachedHighlightingRegexes = new Dictionary<string, Regex>( );
		static readonly Regex EmptyRegex = new Regex( "(?!)" );


		public OnigurumaRegexEngine( )
		{
			OptionsControl = new UCOnigurimaRegexOptions( );
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

				//......
#if false
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
#endif
			}
		}


		public void HighlightPattern( ICancellable cnc, Highlights highlights, string pattern, int selectionStart, int selectionEnd, Segment visibleSegment )
		{

		}

		#endregion


		private void OptionsControl_Changed( object sender, EventArgs e )
		{
			OptionsChanged?.Invoke( this );
		}


		Regex GetCachedColouringRegex( )
		{
			string syntax = OptionsControl.GetSyntax( );
			bool is_plain_text = syntax == "ONIG_SYNTAX_ASIS";

			if( is_plain_text ) return EmptyRegex;

			bool is_extended = OptionsControl.IsOptionSelected( "ONIG_OPTION_EXTEND" );

			string key = string.Join( "\u001F", new object[] { syntax, is_extended } );

			lock( CachedColouringRegexes )
			{
				if( CachedColouringRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				string escape = @"(?'escape'";

				escape += @"\\0[0-7]{1,2} | "; // octal, two digits after 0
				escape += @"\\[0-7]{1,3} | "; // octal, three digits

				escape += @"\\o\{[0-7]+(\}|$) | "; // \o{17777777777} wide octal char

				escape += @"\\u[0-9a-fA-F]+ | "; // \uHHHH wide hexadecimal char
				escape += @"\\x[0-9a-fA-F]+ | "; // \xHH hexadecimal char 
				escape += @"\\x\{[0-9a-fA-F]+(\}|$) | "; // \x{7HHHHHHH} wide hexadecimal char

				escape += @"\\c[A-Za-z] | "; // \cx control char
				escape += @"\\C-([A-Za-z])? | "; // \C-x control char

				escape += @"\\M-([A-Za-z])? | "; // \M-x meta  (x|0x80)
				escape += @"\\M-(\\C-([A-Za-z])?)? | "; // \M-x meta control char

				escape += @"\\[pP]\{.*?(\} | $) | "; // property

				escape += @"\\. | ";

				escape = Regex.Replace( escape, @"\s*\|\s*$", "" );
				escape += ")";

#if false


				// 

				string @class = @"(?'class'";

				@class += @"\[(?'c'[:]) .*? (\k<c>\] | $) | "; // only [: :], no [= =], no [. .]

				@class = Regex.Replace( @class, @"\s*\|\s*$", "" );
				@class += ")";

				//

				string char_group = @"(";

				char_group += @"\[ (" + @class + " | " + escape + " | . " + @")*? (\]|$) | "; // TODO: check 'escape' part

				char_group = Regex.Replace( char_group, @"\s*\|\s*$", "" );
				char_group += ")";

				// 

#endif

				string comment = @"(?'comment'";

				if( Any( syntax, "ONIG_SYNTAX_JAVA", "ONIG_SYNTAX_PERL", "ONIG_SYNTAX_PERL_NG", "ONIG_SYNTAX_RUBY", "ONIG_SYNTAX_ONIGURUMA" ) )
				{
					comment += @"\(\?\#.*?(\)|$) | "; // comment
				}

				if( is_extended ) comment += @"\#.*?(\n|$) | "; // line-comment

				comment = Regex.Replace( comment, @"\s*\|\s*$", "" );
				comment += ")";

#if false
				//

				string named_group = @"(?'named_group'";

				named_group += @"\(\?P(?'name'<.*?>) | ";

				named_group = Regex.Replace( named_group, @"\s*\|\s*$", "" );
				named_group += ")";

#endif
				string pattern = @"(?nsx)(" + Environment.NewLine +
					escape + " | " + Environment.NewLine +
					comment + " | " + Environment.NewLine +
					//........char_group + " | " + Environment.NewLine +
					//.........named_group + " | " + Environment.NewLine +
					"(.(?!)) )";

				regex = new Regex( pattern, RegexOptions.Compiled );

				CachedColouringRegexes.Add( key, regex );

				return regex;
			}
		}


		Regex GetCachedHighlightingRegex( )
		{
			bool is_literal = OptionsControl.IsOptionSelected( "literal" );

			if( is_literal ) return EmptyRegex;

			string key = string.Join( "\u001F", new object[] { "" } ); // (no variants yet)

			lock( CachedHighlightingRegexes )
			{
				if( CachedHighlightingRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				string pattern = @"(?nsx)(";

				pattern += @"(?'left_para'\() | "; // '('
				pattern += @"(?'right_para'\)) | "; // ')'
				pattern += @"(?'range'\{\d+(,(\d+)?)?(\}(?'end')|$)) | "; // '{...}'

				pattern += @"(?'char_group'\[ ((\[:.*? (:\]|$)) | \\. | .)*? (\](?'end')|$) ) | "; // (including incomplete classes)
				pattern += @"\\. | . | ";

				pattern = Regex.Replace( pattern, @"\s*\|\s*$", "" );
				pattern += @")";

				regex = new Regex( pattern, RegexOptions.Compiled );

				CachedHighlightingRegexes.Add( key, regex );

				return regex;
			}
		}


		static bool Any( string s, params string[] values )
		{
			return values != null && values.Contains( s );
		}

	}
}