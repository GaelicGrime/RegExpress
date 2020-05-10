using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure
{
	[Flags]
	public enum RegexEngineCapabilityEnum
	{
		Default = 0,
		NoCaptures = ( 1 << 1 ),
		CombineSurrogatePairs = ( 1 << 2 ),
	}
}
