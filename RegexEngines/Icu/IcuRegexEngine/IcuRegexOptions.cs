using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace IcuRegexEngineNs
{
	public sealed class IcuRegexOptions
	{
		public bool UREGEX_CANON_EQ { get; set; }
		public bool UREGEX_CASE_INSENSITIVE { get; set; } // "i"
		public bool UREGEX_COMMENTS { get; set; } // "x"
		public bool UREGEX_DOTALL { get; set; } // "s"
		public bool UREGEX_LITERAL { get; set; }
		public bool UREGEX_MULTILINE { get; set; } // "m"
		public bool UREGEX_UNIX_LINES { get; set; }
		public bool UREGEX_UWORD { get; set; } // "w"
		public bool UREGEX_ERROR_ON_UNKNOWN_ESCAPES { get; set; }


		public string Limit { get; set; }


		public IcuRegexOptions Clone( )
		{
			return (IcuRegexOptions)MemberwiseClone( );
		}
	}
}
