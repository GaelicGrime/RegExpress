using System.Collections.Generic;
using RegexEngineInfrastructure.Matches;


namespace RegexEngineInfrastructure.Matches
{
	public abstract class RegexGroup : RegexCapture
	{
		public abstract bool Success { get; }

		public abstract string Name { get; }

		public abstract IEnumerable<RegexCapture> Captures { get; }
	}

}
