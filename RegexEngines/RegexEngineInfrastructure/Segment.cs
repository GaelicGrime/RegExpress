using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure
{
	public class Segment
	{
		public int Index { get; }
		public int Length { get; }

		public Segment( int index, int length )
		{
			Debug.Assert( length >= 0 );

			Index = index;
			Length = length;
		}

		public bool IsEmpty => Length == 0;
		public int End => Index + Length;

		public static Segment Empty => new Segment( 0, 0 );


		public static Segment Intersection( Segment a, Segment b )
		{
			return Intersection( a, b.Index, b.Length );
		}


		public static Segment Intersection( Segment a, int bIndex, int bLength )
		{
			var i = Math.Max( a.Index, bIndex );
			var e = Math.Min( a.End, bIndex + bLength );

			if( e < i ) return Empty;

			return new Segment( i, e - i );
		}


		#region Object

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

		#endregion


		/* ?

		public static bool operator ==( Segment left, Segment right )
		{
			return left.Equals( right );
		}


		public static bool operator !=( Segment left, Segment right )
		{
			return !( left == right );
		}

		*/

	}
}
