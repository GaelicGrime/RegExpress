using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace WebView2RegexEngineNs
{
	public class WebView2RegexOptions
	{
		public bool i { get; set; }
		public bool m { get; set; }
		public bool s { get; set; }
		public bool u { get; set; }


		public WebView2RegexOptions Clone( )
		{
			return (WebView2RegexOptions)MemberwiseClone( );
		}
	}
}
