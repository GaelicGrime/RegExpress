using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


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


	public static class RegexUtilities
	{
		static readonly Regex EndGroupRegex = new Regex( @"(\s*\|\s*)?$", RegexOptions.ExplicitCapture | RegexOptions.Compiled );


		public static string EndGroup( string s, string name )
		{
			if( string.IsNullOrWhiteSpace( s ) ) return null;

			if( name != null )
			{
				s = "(?'" + name + "'" + EndGroupRegex.Replace( s, ")", 1 );
			}
			else
			{
				s = "(" + EndGroupRegex.Replace( s, ")", 1 );
			}

			return s;
		}

	}
}
