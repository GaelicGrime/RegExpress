using DotNetRegexEngine.Matches;
using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace DotNetRegexEngine
{
	public class DotNetRegexEngine : RegexEngine
	{

		class DotNetRegexOptionInfo : RegexOptionInfo
		{
			public DotNetRegexOptionInfo( RegexOptions option, string note )
			{
				Note = note;
				RegexOption = option;
			}


			public RegexOptions RegexOption { get; }


			#region RegexOptionInfo

			public override string Text
			{
				get
				{
					return RegexOption.ToString( );
				}
			}

			public override string Note { get; }

			public override string AsText
			{
				get
				{
					return RegexOption.ToString( );
				}
			}

			#endregion RegexOptionInfo
		}



		#region RegexEngine

		public override IReadOnlyCollection<RegexOptionInfo> AllOptions
		{
			get
			{
				return new List<RegexOptionInfo>
				{
					MakeOptionInfo( RegexOptions.CultureInvariant ),
					MakeOptionInfo( RegexOptions.ECMAScript ),
					MakeOptionInfo( RegexOptions.ExplicitCapture ),
					MakeOptionInfo( RegexOptions.IgnoreCase ),
					MakeOptionInfo( RegexOptions.IgnorePatternWhitespace ),
					MakeOptionInfo( RegexOptions.Multiline, "('^', '$' at '\\n' too)" ),
					MakeOptionInfo( RegexOptions.RightToLeft ),
					MakeOptionInfo( RegexOptions.Singleline, "('.' matches '\\n' too)" ),
				};
			}
		}


		public override object ParsePattern( string pattern, IReadOnlyCollection<RegexOptionInfo> options )
		{
			RegexOptions regex_options = RegexOptions.None;

			foreach( DotNetRegexOptionInfo opt in options )
			{
				regex_options |= opt.RegexOption;
			}

			return new Regex( pattern, regex_options );
		}

		public override IEnumerable<RegexMatch> Matches( object parsingResult, string text )
		{
			Regex regex = (Regex)parsingResult;

			return regex.Matches( text ).OfType<Match>( ).Select( m => new DotNetRegexMatch( m ) );
		}






		#endregion RegexBase


		RegexOptionInfo MakeOptionInfo( RegexOptions option, string note = null )
		{
			return new DotNetRegexOptionInfo( option, note );
		}
	}
}
