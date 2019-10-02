using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegExpressWPF.Code
{
	public struct Segment
	{
#pragma warning disable CA1051 // Do not declare visible instance fields

		public readonly int Index;
		public readonly int Length;

#pragma warning restore CA1051 // Do not declare visible instance fields

		public Segment( int index, int length )
		{
			Index = index;
			Length = length;
		}


		public override string ToString( )
		{
			return Length == 0 ? $"(empty at {Index})" : $"({Index}..{Index + Length - 1})";
		}


		public override bool Equals( object obj )
		{
			if( !( obj is Segment ) ) return false;

			Segment a = (Segment)obj;

			return Index == a.Index && Length == a.Length;
		}


		public override int GetHashCode( )
		{
			return unchecked(Index ^ Length);
		}


		public static bool operator ==( Segment left, Segment right )
		{
			return left.Equals( right );
		}


		public static bool operator !=( Segment left, Segment right )
		{
			return !( left == right );
		}
	}
}
