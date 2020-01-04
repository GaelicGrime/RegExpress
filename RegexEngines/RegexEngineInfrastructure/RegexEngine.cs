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
		public abstract IReadOnlyCollection<RegexOptionInfo> AllOptions { get; }

		// TODO: avoid 'object'
		public abstract object ParsePattern( string pattern, IReadOnlyCollection<RegexOptionInfo> options );

		// TODO: avoid 'object'
		public abstract IEnumerable<RegexMatch> Matches( object parsingResult, string text );
	}
}
