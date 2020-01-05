using System.Collections.Generic;
using RegexEngineInfrastructure.Matches;


namespace RegexEngineInfrastructure.Matches
{
	public interface IGroup : ICapture
	{
		bool Success { get; }

		string Name { get; }

		IEnumerable<ICapture> Captures { get; }
	}

}
