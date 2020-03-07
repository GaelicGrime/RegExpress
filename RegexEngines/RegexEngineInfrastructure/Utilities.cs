using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure
{
	public static class CheckedCast
	{
		public static Int32 ToInt32( UInt64 v )
		{
			return checked((Int32)v);
		}


		public static Int32 ToInt32( Int64 v )
		{
			return checked((Int32)v);
		}


		public static Int32 ToInt32( UInt32 v )
		{
			return checked((Int32)v);
		}


		[Obsolete( "This should not be achieved.", error: true )]
		public static void ToInt32<T>( T v )
		{
		}



	}
}
