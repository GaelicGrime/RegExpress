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
		static readonly Regex EmptyRegex = new Regex( "(?!)", RegexOptions.Compiled | RegexOptions.ExplicitCapture );


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


		private void OptionsControl_Changed( object sender, RegexEngineOptionsChangedArgs args )
		{
			OptionsChanged?.Invoke( this, args );
		}


		Regex GetCachedColouringRegex( )
		{
			bool is_literal = OptionsControl.IsCompileOptionSelected( "PCRE2_LITERAL" );

			if( is_literal ) return EmptyRegex;

			bool is_extended = OptionsControl.IsCompileOptionSelected( "PCRE2_EXTENDED" );
			bool allow_empty_class = OptionsControl.IsCompileOptionSelected( "PCRE2_ALLOW_EMPTY_CLASS" );

			string key = string.Join( "\u001F", new object[] { is_extended, allow_empty_class } );

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
			bool allow_empty_class = OptionsControl.IsCompileOptionSelected( "PCRE2_ALLOW_EMPTY_CLASS" );

			string key = string.Join( "\u001F", new object[] { is_extended, allow_empty_class } );

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
			bool allow_empty_class = OptionsControl.IsCompileOptionSelected( "PCRE2_ALLOW_EMPTY_CLASS" );

			var pb_escape = new PatternBuilder( );

			pb_escape.BeginGroup( "escape" );

			pb_escape.Add( @"\\c[A-Za-z]" ); // ASCII escape
			pb_escape.Add( @"\\0[0-7]{1,2}" ); // octal, two digits after 0
			pb_escape.Add( @"\\[0-7]{1,3}" ); // octal, three digits
			pb_escape.Add( @"\\o\{[0-9]+(\} | $)" ); // octal; bad values give error
			pb_escape.Add( @"\\N\{U\+[0-9a-fA-F]+(\} | $)" ); // hexa, error if no 'PCRE2_UTF'
			pb_escape.Add( @"\\x[0-9a-fA-F]{1,2}" ); // hexa, two digits
			pb_escape.Add( @"\\x\{[0-9a-fA-F]*(\} | $)" ); // hexa, error if empty
			pb_escape.Add( @"\\u[0-9a-fA-F]{1,4}" ); // hexa, four digits, error if no 'PCRE2_ALT_BSUX', 'PCRE2_EXTRA_ALT_BSUX'
			pb_escape.Add( @"\\u\{[0-9a-fA-F]*(\} | $)" ); // hexa, error if empty or no 'PCRE2_ALT_BSUX', 'PCRE2_EXTRA_ALT_BSUX'
			pb_escape.Add( @"\\[pP]\{.*?(\} | $)" ); // property
			pb_escape.Add( @"\\Q.*?(\\E|$)" ); // quoted sequence, \Q...\E

			// backreferences
			pb_escape.Add( @"\\[0-9]+" ); // unbiguous
										  // see also named groups

			pb_escape.Add( @"\\." );

			pb_escape.EndGroup( );

			// 

			var pb_class = new PatternBuilder( ).AddGroup( "class", @"\[(?'c'[:=.]) .*? (\k<c>\] | $)" );

			//

			var pb = new PatternBuilder( );

			pb.BeginGroup( "comment" );

			pb.Add( @"\(\?\#.*?(\)|$)" ); // comment
			if( is_extended ) pb.Add( @"\#.*?(\n|$)" ); // line-comment

			pb.EndGroup( );

			//

			pb.Add( @"\(\?(?'name'<(?![=!]).*?(>|$))" );
			pb.Add( @"\(\?(?'name''.*?('|$))" );
			pb.Add( @"\(\?P(?'name'<.*?(>|$))" );
			pb.Add( @"(?'name'\\g[+]?[0-9]+)" );
			pb.Add( @"(?'name'\\g\{[+]?[0-9]*(\} | $))" );
			pb.Add( @"(?'name'\\[gk]<.*?(>|$))" );
			pb.Add( @"(?'name'\\[gk]'.*?('|$))" );
			pb.Add( @"(?'name'\\[gk]\{.*?(\}|$))" );
			pb.Add( @"(?'name'\(\?P=.*?(\)|$))" ); //


			//

			pb.Add( pb_escape.ToPattern( ) );

			//

			string char_group;

			if( allow_empty_class )
				char_group = $@"\[     ({pb_class.ToPattern( )} | {pb_escape.ToPattern( )} | . )*? (\]|$)";
			else
				char_group = $@"\[ \]? ({pb_class.ToPattern( )} | {pb_escape.ToPattern( )} | . )*? (\]|$)";

			pb.Add( char_group );


			// TODO: add support for '(*...)' constructs

			return pb.ToRegex( );
		}


		Regex CreateHighlightingRegex( )
		{
			bool is_literal = OptionsControl.IsCompileOptionSelected( "PCRE2_LITERAL" );

			if( is_literal ) return EmptyRegex;

			bool is_extended = OptionsControl.IsCompileOptionSelected( "PCRE2_EXTENDED" );
			bool allow_empty_class = OptionsControl.IsCompileOptionSelected( "PCRE2_ALLOW_EMPTY_CLASS" );

			var pb = new PatternBuilder( );

			pb.Add( @"(\(\?\#.*?(\)|$))" ); // comment
			if( is_extended ) pb.Add( @"(\#[^\n]*)" ); // line comment
			pb.Add( @"\\Q.*?(\\E|$)" ); // quoted sequence, \Q...\E
			pb.Add( @"\\[oNxupP]\{.*?(\}|$)" ); // (skip)

			pb.Add( @"(?'left_par'\()" ); // '('
			pb.Add( @"(?'right_par'\))" ); // ')'
			pb.Add( @"(?'left_brace'\{) \d+ (,\d*)? ((?'right_brace'\})|$)" ); // '{...}'
			if( allow_empty_class )
				pb.Add( @"((?'left_bracket'\[)     ((\[:.*? (:\]|$)) | \\. | .)*? ((?'right_bracket'\])|$) )" ); // [...]
			else
				pb.Add( @"((?'left_bracket'\[) \]? ((\[:.*? (:\]|$)) | \\. | .)*? ((?'right_bracket'\])|$) )" ); // [...]
			pb.Add( @"\\." ); // '\...'

			return pb.ToRegex( );
		}

	}
}
