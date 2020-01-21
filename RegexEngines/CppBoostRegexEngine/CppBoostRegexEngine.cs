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


namespace CppBoostRegexEngineNs
{
	public class CppBoostRegexEngine : IRegexEngine
	{
		readonly UCCppBoostRegexOptions OptionsControl;

		static readonly Dictionary<GrammarEnum, Regex> CachedColouringRegexes = new Dictionary<GrammarEnum, Regex>( );
		static readonly Dictionary<GrammarEnum, Regex> CachedHighlightingRegexes = new Dictionary<GrammarEnum, Regex>( );


		public CppBoostRegexEngine( )
		{
			OptionsControl = new UCCppBoostRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "CppBoostRegex";

		public string Name => "C++ Boost Regex";

		public event EventHandler OptionsChanged;


		public Control GetOptionsControl( )
		{
			return OptionsControl;
		}


		public object SerializeOptions( )
		{
			return OptionsControl.ToSerialisableObject( );
		}


		public void DeserializeOptions( object obj )
		{
			OptionsControl.FromSerializableObject( obj );
		}


		public IMatcher ParsePattern( string pattern )
		{
			var selected_options = OptionsControl.CachedOptions;

			return new CppBoostRegexInterop.CppMatcher( pattern, selected_options );
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
					}
				}

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
			}
		}


		public Highlights HighlightPattern( ICancellable cnc, string pattern, int startSelection, int endSelection, Segment visibleSegment )
		{
			// TODO: implement

			return null;
		}

		#endregion IRegexEngine


		private void OptionsControl_Changed( object sender, EventArgs e )
		{
			OptionsChanged?.Invoke( this, null );
		}



		static Regex GetCachedColouringRegex( GrammarEnum grammar )
		{
			lock( CachedColouringRegexes )
			{
				if( CachedColouringRegexes.TryGetValue( grammar, out Regex regex ) ) return regex;

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

				string escape = @"(?'escape'";

				if( is_perl || is_POSIX_extended || is_POSIX_basic ) escape += @"\\[1-9] | "; // back reference
				if( is_perl ) escape += @"\\g-?[1-9] | \\g\{.*?\} | "; // back reference
				if( is_perl ) escape += @"\\k<.*?(>|$) | "; // back reference

				if( is_perl || is_POSIX_extended ) escape += @"\\c[A-Za-z] | "; // ASCII escape
				if( is_perl || is_POSIX_extended ) escape += @"\\x[0-9A-Fa-f]{1,2} | "; // hex, two digits
				if( is_perl || is_POSIX_extended ) escape += @"\\x\{[0-9A-Fa-f]+(\}|$) | "; // hex, four digits
				if( is_perl || is_POSIX_extended ) escape += @"\\0[0-7]{1,3} | "; // octal, three digits
				if( is_perl || is_POSIX_extended ) escape += @"\\N\{.*?(\}|$) | "; // symbolic name
				if( is_perl || is_POSIX_extended ) escape += @"\\[pP]\{.*?(\}|$) | "; // property
				if( is_perl || is_POSIX_extended ) escape += @"\\[pP]. | "; // property, short name
				if( is_perl || is_POSIX_extended ) escape += @"\\Q.*?(\\E|$) | "; // quoted sequence
				if( is_emacs ) escape += @"\\[sS]. | "; // syntax group

				if( is_perl || is_POSIX_extended || is_POSIX_basic ) escape += @"\\. | "; // various

				escape = Regex.Replace( escape, @"\s*\|\s*$", "" );
				escape += ")";

				// 

				string comment = @"(?'comment'";

				if( is_perl ) comment += @"\(\?\#.*?(\)|$) | "; // comment
				/*if(  ) comment += @"\#.*?(\n|$) | "; // line-comment*/

				comment = Regex.Replace( comment, @"\s*\|\s*$", "" );
				comment += ")";

				// 

				string @class = @"(?'class'";

				if( is_perl || is_POSIX_extended || is_POSIX_basic ) @class += @"\[(?'c'[:=.]) .*? (\k<c>\] | $) | ";

				@class = Regex.Replace( @class, @"\s*\|\s*$", "" );
				@class += ")";

				//

				string char_group = @"(";

				if( is_perl || is_POSIX_basic ) char_group += @"\[ (" + @class + " | " + escape + " | . " + @")*? (\]|$) | ";

				char_group = Regex.Replace( char_group, @"\s*\|\s*$", "" );
				char_group += ")";

				//

				string named_group = @"(?'named_group'";

				if( is_perl ) named_group += @"\(\?(?'name'((?'a'')|<).*?(?(a)'|>))";

				named_group = Regex.Replace( named_group, @"\s*\|\s*$", "" );
				named_group += ")";


				// 

				string pattern = @"(?nsx)(" + Environment.NewLine +
					escape + " | " + Environment.NewLine +
					comment + " | " + Environment.NewLine +
					char_group + " | " + Environment.NewLine +
					named_group + " | " + Environment.NewLine +
					"(.(?!)) )";

				regex = new Regex( pattern, RegexOptions.Compiled );

				CachedColouringRegexes.Add( grammar, regex );

				return regex;
			}
		}
	}
}
