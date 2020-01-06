using RegexEngineInfrastructure.Matches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure
{
	public interface IParsedPattern
	{
		RegexMatches Matches( string text );
	}
}
