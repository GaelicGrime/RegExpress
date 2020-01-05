using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegexEngineInfrastructure.Matches
{
	public class RegexMatches
	{
		public int Count { get; }
		public IEnumerable<IMatch> Matches { get; }

		public RegexMatches( int count, IEnumerable<IMatch> matches )
		{
			Count = count;
			Matches = matches;
		}


		public static RegexMatches Empty { get; } = new RegexMatches( 0, Enumerable.Empty<IMatch>( ) );
	}
}
