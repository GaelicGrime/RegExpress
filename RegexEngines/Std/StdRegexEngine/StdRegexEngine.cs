﻿using RegexEngineInfrastructure;
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


namespace StdRegexEngineNs
{
	public class StdRegexEngine : IRegexEngine
	{
		readonly UCStdRegexOptions OptionsControl;

		static readonly Dictionary<GrammarEnum, Regex> CachedColouringRegexes = new Dictionary<GrammarEnum, Regex>( );
		static readonly Dictionary<GrammarEnum, Regex> CachedHighlightingRegexes = new Dictionary<GrammarEnum, Regex>( );


		public StdRegexEngine( )
		{
			OptionsControl = new UCStdRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "CppStdRegex";

		public string Name => "std::wregex";

		public string EngineVersion => StdRegexInterop.Matcher.GetCRTVersion( );

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

			return new StdRegexInterop.Matcher( pattern, selected_options );
		}


		public void ColourisePattern( ICancellable cnc, ColouredSegments colouredSegments, string pattern, Segment visibleSegment )
		{
			GrammarEnum grammar = OptionsControl.GetGrammar( );

			Regex regex = GetCachedColouringRegex( grammar );

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

				// classes within character groups, [ ... [:...:] ... ]
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
			}
		}


		public void HighlightPattern( ICancellable cnc, Highlights highlights, string pattern, int selectionStart, int selectionEnd, Segment visibleSegment )
		{
			GrammarEnum grammar = OptionsControl.GetGrammar( );
			int par_size = 1;
			int bracket_size = 1;

			if( grammar == GrammarEnum.basic ||
				grammar == GrammarEnum.grep )
			{
				par_size = 2;
			}

			var regex = GetCachedHighlightingRegex( grammar );

			HighlightHelper.CommonHighlighting( cnc, highlights, pattern, selectionStart, selectionEnd, visibleSegment, regex, par_size, bracket_size );
		}

		#endregion IRegexEngine


		private void OptionsControl_Changed( object sender, RegexEngineOptionsChangedArgs args )
		{
			OptionsChanged?.Invoke( this, args );
		}



		static Regex GetCachedColouringRegex( GrammarEnum grammar )
		{
			lock( CachedColouringRegexes )
			{
				if( CachedColouringRegexes.TryGetValue( grammar, out Regex regex ) ) return regex;

				regex = CreateColouringRegex( grammar );

				CachedColouringRegexes.Add( grammar, regex );

				return regex;
			}
		}


		static Regex GetCachedHighlightingRegex( GrammarEnum grammar )
		{
			lock( CachedHighlightingRegexes )
			{
				if( CachedHighlightingRegexes.TryGetValue( grammar, out Regex regex ) ) return regex;

				regex = CreateHighlightingRegex( grammar );

				CachedHighlightingRegexes.Add( grammar, regex );

				return regex;
			}
		}


		static Regex CreateColouringRegex( GrammarEnum grammar )
		{
			string escape = @"(?'escape'";

			if( grammar == GrammarEnum.ECMAScript ) escape += @"\\c[A-Za-z] | ";
			if( grammar == GrammarEnum.ECMAScript ) escape += @"\\x[0-9A-Fa-f]{1,2} | "; // (two digits required)
			if( grammar == GrammarEnum.awk ) escape += @"\\[0-7]{1,3} | "; // octal code
			if( grammar == GrammarEnum.ECMAScript ) escape += @"\\u[0-9A-Fa-f]{1,4} | "; // (four digits required)

			if( grammar == GrammarEnum.basic ||
				grammar == GrammarEnum.grep )
			{
				escape += @"(?!\\\( | \\\) | \\\{ | \\\})\\.";
			}
			else
			{
				escape += @"\\.";
			}
			escape += @")";

			string @class = @"(?'class' \[(?'c'[:=.]) .*? (\k<c>\] | $) )";

			string char_group = @"( \[ (" + @class + " | " + escape + " | . " + @")*? (\]|$) )";

			// (group names and comments are not supported by C++ Regex)

			string pattern = @"(?nsx)(" + Environment.NewLine +
				escape + " | " + Environment.NewLine +
				char_group + " | " + Environment.NewLine +
				")";

			var regex = new Regex( pattern, RegexOptions.Compiled );

			return regex;
		}


		static Regex CreateHighlightingRegex( GrammarEnum grammar )
		{
			string pattern = @"(?nsx)(";

			if( grammar == GrammarEnum.extended ||
				grammar == GrammarEnum.ECMAScript ||
				grammar == GrammarEnum.egrep ||
				grammar == GrammarEnum.awk )
			{
				pattern += @"(?'left_par'\() | "; // '('
				pattern += @"(?'right_par'\)) | "; // ')'
				pattern += @"(?'left_brace'\{).*?((?'right_brace'\})|$) | "; // '{...}'
			}

			if( grammar == GrammarEnum.basic ||
				grammar == GrammarEnum.grep )
			{
				pattern += @"(?'left_par'\\\() | "; // '\)'
				pattern += @"(?'right_par'\\\)) | "; // '\('
				pattern += @"(?'left_brace'\\{).*?((?'right_brace'\\})|$) | "; // '\{...\}'
			}

			pattern += @"((?'left_bracket'\[) ((\[:.*? (:\]|$)) | \\. | .)*? ((?'right_bracket'\])|$) ) | "; // [...]
			pattern += @"\\.";  // '\...'

			pattern += @")";

			var regex = new Regex( pattern, RegexOptions.Compiled );

			return regex;
		}
	}
}
