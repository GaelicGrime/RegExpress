using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RegexEngineInfrastructure.Matches;


namespace RegexEngineInfrastructure.Matches
{
	public interface IMatch : IGroup
	{
		IEnumerable<IGroup> Groups { get; }
	}
}
