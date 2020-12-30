using System;


namespace RegexEngineInfrastructure
{
	public static class CheckedCast
	{
		public static Int32 ToInt32( UInt64 v )
		{
			return checked((Int32)v);
		}


		public static Int32 ToInt32n( UInt64 v )
		{
			if( v == UInt64.MaxValue ) return -1;

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
