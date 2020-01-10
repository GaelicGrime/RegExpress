using RegexEngineInfrastructure.Matches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure
{
	public interface IRegexEngine
	{
		string Id { get; }

		IReadOnlyCollection<IRegexOptionInfo> AllOptions { get; }

		IMatcher ParsePattern( string pattern, IReadOnlyCollection<IRegexOptionInfo> options );
	}
}
