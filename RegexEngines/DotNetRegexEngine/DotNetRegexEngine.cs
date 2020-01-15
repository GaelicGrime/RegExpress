using DotNetRegexEngineNs.Matches;
using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.SyntaxColouring;
using System;
using System.Collections.Generic;
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
(?'eol_comment'\#[^n]*) |
\\. | .
)";

		const string NoIgnoreWhitespacePattern = @"
..............
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


		public ICollection<SyntaxHighlightSegment> ColourisePattern( string pattern, int start, int length )
		{
			// TODO: implement

			bool ignore_pattern_whitespaces = OptionsControl.CachedRegexOptions.HasFlag( RegexOptions.IgnorePatternWhitespace );
			Regex re = ignore_pattern_whitespaces ? ReIgnorePatternWhitespace : ReNoIgnorePatternWhitespace;


			foreach(Match m in re.Matches(pattern))
			{
				if( m.Index + m.Length < start ) continue;
				if( m.Index + m.Length > start + length ) continue;


			}

			return null;
		}

		#endregion IRegexEngine


		private void OptionsControl_Changed( object sender, EventArgs e )
		{
			OptionsChanged?.Invoke( this, null );
		}


	}
}
