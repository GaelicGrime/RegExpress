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


		private void OptionsControl_Changed( object sender, RegexEngineOptionsChangedArgs args )
		{
			OptionsChanged?.Invoke( this, args );
		}



		Regex GetCachedColouringRegex( )
		{
			bool is_literal = OptionsControl.IsOptionSelected( "literal" );

			if( is_literal ) return PatternBuilder.AlwaysFailsRegex;

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

			if( is_literal ) return PatternBuilder.AlwaysFailsRegex;

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

			if( is_literal ) return PatternBuilder.AlwaysFailsRegex;

			var pb_escape = new PatternBuilder( );

			pb_escape.BeginGroup( "escape" );

			pb_escape.Add( @"\\[pP][A-Za-z]" ); // Unicode character class (one-letter name)
			pb_escape.Add( @"\\[pP]\{.*?(\}|$)" ); // Unicode character class
			pb_escape.Add( @"\\0[0-7]{1,2}" ); // octal, two digits after 0
			pb_escape.Add( @"\\[0-7]{1,3}" ); // octal, three digits
			pb_escape.Add( @"\\x[0-9a-fA-F]{1,2}" ); // hexa, two digits
			pb_escape.Add( @"\\x\{[0-9a-fA-F]*(\}|$)" ); // hexa, error if empty
			pb_escape.Add( @"\\Q.*?(\\E|$)" ); // quoted sequence, \Q...\E
			pb_escape.Add( @"\\." );

			pb_escape.EndGroup( );

			//

			var pb_class = new PatternBuilder( ).AddGroup( "class", @"\[(?'c'[:]) .*? (\k<c>\] | $)" ); // only [: :], no [= =], no [. .]

			//

			var pb = new PatternBuilder( );

			pb.Add( pb_escape.ToPattern( ) );

			//

			pb.AddGroup( null, $@"\[ \]? ({pb_class.ToPattern( )} | {pb_escape.ToPattern( )} | . )*? (\]|$)" ); // TODO: check 'escape' part

			// 

			pb.Add( @"\(\?P(?'name'<.*?>)" );

			return pb.ToRegex( );
		}


		Regex CreateHighlightingRegex( )
		{
			bool is_literal = OptionsControl.IsOptionSelected( "literal" );

			if( is_literal ) return PatternBuilder.AlwaysFailsRegex;

			var pb = new PatternBuilder( );

			pb.Add( @"\\Q.*?(\\E|$)" ); // quoted sequence, \Q...\E
			pb.Add( @"\\[pPx]\{.*?(\}|$)" ); // (skip)
			pb.Add( @"(?'left_par'\()" ); // '('
			pb.Add( @"(?'right_par'\))" ); // ')'
			pb.Add( @"(?'left_brace'\{) \d+ (,\d*)* ((?'right_brace'\})|$)" ); // '{...}'
			pb.Add( @"((?'left_bracket'\[) \]? ((\[:.*? (:\]|$)) | \\. | .)*? ((?'right_bracket'\])|$) )" );
			pb.Add( @"\\." ); // '\...'

			return pb.ToRegex( );
		}

	}
}
