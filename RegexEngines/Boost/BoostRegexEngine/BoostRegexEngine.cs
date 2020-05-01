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

		public string NoteForCaptures => "requires ‘match_extra’";

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

			int par_size = 1;
			int bracket_size = 1;

			bool is_POSIX_basic =
				grammar == GrammarEnum.basic ||
				grammar == GrammarEnum.sed ||
				grammar == GrammarEnum.grep ||
				grammar == GrammarEnum.emacs;

			if( is_POSIX_basic )
			{
				par_size = 2;
			}

			Regex regex = GetCachedHighlightingRegex( grammar, mod_x );

			HighlightHelper.CommonHighlighting( cnc, highlights, pattern, selectionStart, selectionEnd, visibleSegment, regex, par_size, bracket_size );
		}

		#endregion IRegexEngine


		private void OptionsControl_Changed( object sender, RegexEngineOptionsChangedArgs args )
		{
			OptionsChanged?.Invoke( this, args );
		}


		static Regex GetCachedColouringRegex( GrammarEnum grammar, bool modX )
		{
			var key = new Key { Grammar = grammar, ModX = modX };

			lock( CachedColouringRegexes )
			{
				if( CachedColouringRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateColouringRegex( grammar, modX );

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

				regex = CreateHighlightingRegex( grammar, modX );

				CachedHighlightingRegexes.Add( key, regex );

				return regex;
			}
		}


		static Regex CreateColouringRegex( GrammarEnum grammar, bool modX )
		{
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


			string escape = "";

			if( is_perl || is_POSIX_extended || is_POSIX_basic ) escape += @"\\[1-9] | "; // back reference

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

			escape = RegexUtilities.EndGroup( escape, "escape" );

			// 

			string comment = "";

			if( is_perl ) comment += @"\(\?\#.*?(\)|$) | "; // comment
			if( is_perl && modX ) comment += @"\#.*?(\n|$) | "; // line-comment*/

			comment = RegexUtilities.EndGroup( comment, "comment" );

			// 

			string @class = "";

			if( is_perl || is_POSIX_extended || is_POSIX_basic ) @class += @"\[(?'c'[:=.]) .*? (\k<c>\] | $) | ";

			@class = RegexUtilities.EndGroup( @class, "class" );

			//

			string char_group = "";

			if( is_perl || is_POSIX_extended || is_POSIX_basic ) char_group += @"\[ \]? (" + @class + " | " + escape + " | . " + @")*? (\]|$) | ";

			char_group = RegexUtilities.EndGroup( char_group, null );

			//

			string named_group = "";

			if( is_perl ) named_group += @"\(\?(?'name'<(?![=!]).*?(>|$)) | \(\?(?'name''.*?('|$)) | ";
			if( is_perl ) named_group += @"(?'name'\\g-?[1-9]) | (?'name'\\g\{.*?(\}|$)) | "; // back reference
			if( is_perl ) named_group += @"(?'name'\\[gk]<.*?(>|$)) | (?'name'\\[gk]'.*?('|$)) | "; // back reference

			named_group = RegexUtilities.EndGroup( named_group, "named_group" );

			// 

			string[] all = new[]
			{
				comment,
				named_group,
				char_group,
				escape,
			};

			string pattern = @"(?nsx)(" + Environment.NewLine +
				string.Join( " | " + Environment.NewLine, all.Where( s => !string.IsNullOrWhiteSpace( s ) ) ) +
				")";

			var regex = new Regex( pattern, RegexOptions.Compiled | RegexOptions.ExplicitCapture );

			return regex;
		}


		static Regex CreateHighlightingRegex( GrammarEnum grammar, bool modX )
		{
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

			string pattern = "";

			if( is_perl ) pattern += @"(\(\?\#.*?(\)|$)) | "; // comment
			if( is_perl && modX ) pattern += @"(\#[^\n]*) | "; // line comment

			if( is_perl || is_POSIX_extended )
			{
				pattern += @"\\Q.*?(\\E|$) | "; // skip \Q...\E
			}

			if( is_perl || is_POSIX_extended )
			{
				pattern += @"(?'left_par'\() | "; // '('
				pattern += @"(?'right_par'\)) | "; // ')'
				pattern += @"(?'left_brace'\{).*?((?'right_brace'\})|$) | "; // '{...}' (spaces are allowed)
			}

			if( is_POSIX_basic )
			{
				pattern += @"(?'left_par'\\\() | "; // '\('
				pattern += @"(?'right_par'\\\)) | "; // '\)'
				pattern += @"(?'left_brace'\\{).*?((?'right_brace'\\})|$) | "; // '\{...\}'
			}

			if( is_perl || is_POSIX_extended || is_POSIX_basic )
			{
				pattern += @"((?'left_bracket'\[) \]? ((\[:.*? (:\]|$)) | \\. | .)*? ((?'right_bracket'\])|$) ) | "; // [...]
				pattern += @"\\. | "; // '\...'
			}

			pattern = RegexUtilities.EndGroup( pattern, null );

			if( string.IsNullOrWhiteSpace( pattern ) )
				pattern = "(?!)";
			else
				pattern = "(?nsx)" + pattern;

			var regex = new Regex( pattern, RegexOptions.Compiled | RegexOptions.ExplicitCapture );

			return regex;
		}

	}
}
