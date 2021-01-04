using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace IcuRegexEngineNs
{
	sealed class IcuRegexOptions
	{
		public bool i { get; set; }
		public bool x { get; set; }
		public bool s { get; set; }
		public bool m { get; set; }
		public bool w { get; set; }


		public IcuRegexOptions Clone( )
		{
			return (IcuRegexOptions)MemberwiseClone( );
		}
	}
}
