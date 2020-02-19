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


namespace Re2RegexEngineNs
{
	public class Re2RegexEngine : IRegexEngine
	{
		readonly UCRe2RegexOptions OptionsControl;

		static readonly Dictionary<string, Regex> CachedColouringRegexes = new Dictionary<string, Regex>( );
		static readonly Dictionary<string, Regex> CachedHighlightingRegexes = new Dictionary<string, Regex>( );
		static readonly Regex EmptyRegex = new Regex( "(?!)" );


		public Re2RegexEngine( )
		{
			OptionsControl = new UCRe2RegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;

		}


		#region IRegexEngine

		public string Id => "CppRe2Regex";

		public string Name => "RE2";

		public string EngineVersion => Re2RegexInterop.Matcher.GetRe2Version( );

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

			return new Re2RegexInterop.Matcher( pattern, selected_options );
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

				// class (within [...] groups), '[:...:]'
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

				// named group, '(?P<name>...)'
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

		#endregion


		private void OptionsControl_Changed( object sender, EventArgs e )
		{
			OptionsChanged?.Invoke( this );
		}



		Regex GetCachedColouringRegex( )
		{
			bool is_literal = OptionsControl.IsOptionSelected( "literal" );

			if( is_literal ) return EmptyRegex;

			string key = string.Join( "\u001F", new object[] { "" } ); // (no variants yet)

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
			bool is_literal = OptionsControl.IsOptionSelected( "literal" );

			if( is_literal ) return EmptyRegex;

			string key = string.Join( "\u001F", new object[] { "" } ); // (no variants yet)

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
			bool is_literal = OptionsControl.IsOptionSelected( "literal" );

			if( is_literal ) return EmptyRegex;

			string escape = "";

			escape += @"\\[pP][A-Za-z] | "; // Unicode character class (one-letter name)
			escape += @"\\[pP]\{.*?(\}|$) | "; // Unicode character class

			escape += @"\\0[0-7]{1,2} | "; // octal, two digits after 0
			escape += @"\\[0-7]{1,3} | "; // octal, three digits

			escape += @"\\x[0-9a-fA-F]{1,2} | "; // hexa, two digits
			escape += @"\\x\{[0-9a-fA-F]*(\}|$) | "; // hexa, error if empty

			escape += @"\\Q.*?(\\E|$) | "; // quoted sequence, \Q...\E

			escape += @"\\. | ";

			escape = EndGroup( escape, "escape" );

			// 

			string @class = "";

			@class += @"\[(?'c'[:]) .*? (\k<c>\] | $) | "; // only [: :], no [= =], no [. .]

			@class = EndGroup( @class, "class" );

			//

			string char_group = "";

			char_group += @"\[ (" + @class + " | " + escape + " | . " + @")*? (\]|$) | "; // TODO: check 'escape' part

			char_group = EndGroup( char_group, null );

			// 

			string named_group = "";

			named_group += @"\(\?P(?'name'<.*?>) | ";

			named_group = EndGroup( named_group, "named_group" );

			// 

			string[] all = new[]
			{
				escape,
				char_group,
				named_group,
			};

			string pattern = @"(?nsx)(" + Environment.NewLine +
				string.Join( " | " + Environment.NewLine, all.Where( s => !string.IsNullOrWhiteSpace( s ) ) ) +
				")";

			var regex = new Regex( pattern, RegexOptions.Compiled );

			return regex;
		}


		Regex CreateHighlightingRegex( )
		{
			bool is_literal = OptionsControl.IsOptionSelected( "literal" );

			if( is_literal ) return EmptyRegex;

			string pattern = "";

			pattern += @"(?'left_par'\() | "; // '('
			pattern += @"(?'right_par'\)) | "; // ')'
			pattern += @"(?'left_brace'\{).*?((?'right_brace'\})|$) | "; // '{...}'
			pattern += @"((?'left_bracket'\[) ((\[:.*? (:\]|$)) | \\. | .)*? ((?'right_bracket'\])|$) ) | ";
			pattern += @"\\. | "; // '\...'

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
