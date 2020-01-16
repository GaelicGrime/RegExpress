using DotNetRegexEngineNs.Matches;
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


namespace DotNetRegexEngineNs
{
	public class DotNetRegexEngine : IRegexEngine
	{
		readonly UCDotNetRegexOptions OptionsControl;


		const string IgnoreWhitespacePattern = @"(?nsx)
(
(?'comment'\(\?\#.*?(\)|(?'unfinished'$))) |
(?'char_group'\[(\\.|.)*?(\]|(?'unfinished'$))) |
(?'eol_comment'\#[^\n]*) |
\\. | .
)+
";

		const string NoIgnoreWhitespacePattern = @"(?nsx)
(
(?'comment'\(\?\#.*?(\)|(?'unfinished'$))) |
(?'char_group'\[(\\.|.)*?(\]|(?'unfinished'$))) |
(?'eol_comment'(?!)) 
\\. | .
)+
";


		readonly Regex ReIgnorePatternWhitespace = new Regex( IgnoreWhitespacePattern, RegexOptions.Compiled );
		readonly Regex ReNoIgnorePatternWhitespace = new Regex( NoIgnoreWhitespacePattern, RegexOptions.Compiled );

		public DotNetRegexEngine( )
		{
			OptionsControl = new UCDotNetRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "DotNetRegex";

		public string Name => ".NET Regex";

		public event EventHandler OptionsChanged;


		public Control GetOptionsControl( )
		{
			return OptionsControl;
		}


		public RegexOptions RegexOptions => OptionsControl.CachedRegexOptions;


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
			RegexOptions selected_options = OptionsControl.CachedRegexOptions;
			var regex = new Regex( pattern, selected_options );

			return new DotNetMatcher( regex );
		}


		public void ColourisePattern( ICancellable cnc, ColouredSegments colouredSegments, string pattern, Segment visibleSegment )
		{
			bool ignore_pattern_whitespaces = OptionsControl.CachedRegexOptions.HasFlag( RegexOptions.IgnorePatternWhitespace );
			Regex re = ignore_pattern_whitespaces ? ReIgnorePatternWhitespace : ReNoIgnorePatternWhitespace;

			foreach( Match m in re.Matches( pattern ) ) // (only one)
			{
				Debug.Assert( m.Success );

				if( cnc.IsCancellationRequested ) return;

				// comments, '(?#...)'
				{
					var g = m.Groups["comment"];
					if( g.Success )
					{
						foreach( Capture c in g.Captures )
						{
							if( cnc.IsCancellationRequested ) return;

							var intersection = Segment.Intersection( visibleSegment, c.Index, c.Length );

							if(!intersection.IsEmpty)
							{
								colouredSegments.Comments.Add( intersection );
							}
						}
					}
				}

				// end-on-line comments, '#...'
				{
					var g = m.Groups["eol_comment"];
					if( g.Success )
					{
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
			}
		}

		#endregion IRegexEngine


		private void OptionsControl_Changed( object sender, EventArgs e )
		{
			OptionsChanged?.Invoke( this, null );
		}


	}
}
