using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DRegexEngineNs
{
	sealed class DRegexOptions
	{
		//public bool g { get; set; }
		public bool i { get; set; }
		public bool m { get; set; }
		public bool s { get; set; }
		public bool x { get; set; }


		public DRegexOptions Clone( )
		{
			return (DRegexOptions)MemberwiseClone( );
		}
	}
}
