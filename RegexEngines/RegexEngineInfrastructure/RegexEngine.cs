using RegexEngineInfrastructure.Matches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure
{
	public abstract class RegexEngine
	{
		public abstract string Id { get; }

		public abstract IReadOnlyCollection<RegexOptionInfo> AllOptions { get; }

		// TODO: avoid 'object'
		public abstract object ParsePattern( string pattern, IReadOnlyCollection<RegexOptionInfo> options );

		// TODO: avoid 'object'
		public abstract RegexMatches Matches( object parsingResult, string text );
	}
}
