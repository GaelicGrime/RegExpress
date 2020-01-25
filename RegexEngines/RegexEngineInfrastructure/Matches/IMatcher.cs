using RegexEngineInfrastructure.Matches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure.Matches
{
	public interface IMatcher
	{
		RegexMatches Matches( string text );
	}
}
