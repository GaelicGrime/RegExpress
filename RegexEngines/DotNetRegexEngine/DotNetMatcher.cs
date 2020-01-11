using DotNetRegexEngineNs.Matches;
using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace DotNetRegexEngineNs
{
	class DotNetMatcher : IMatcher
	{
		readonly Regex mRegex;

		public DotNetMatcher( Regex regex )
		{
			mRegex = regex;
		}


		#region IParsedPattern

		public RegexMatches Matches( string text )
		{
			MatchCollection dotnet_matches = mRegex.Matches( text );
			IEnumerable<DotNetRegexMatch> matches = dotnet_matches.OfType<Match>( ).Select( m => new DotNetRegexMatch( m ) );

			return new RegexMatches( dotnet_matches.Count, matches );
		}

		#endregion IParsedPattern
	}
}
