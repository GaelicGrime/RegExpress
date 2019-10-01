using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace RegExpressWPF.Code
{
    public sealed class NaiveRanges
    {
        readonly bool[] data; // TODO: consider a 'BitArray'
        // NOTE. 'bool[]' is array of bytes


        public NaiveRanges( int length )
        {
            data = new bool[length];
        }


        public NaiveRanges( NaiveRanges other )
        {
            data = new bool[other.data.Length];
            Array.Copy( other.data, data, other.data.Length );
        }


        public void Set( int index )
        {
            data[index] = true;
        }


        public void Set( int index, int length )
        {
            var end = index + length;
            while( index < end ) data[index++] = true;
        }


        public void Set( Segment segment )
        {
            Set( segment.Index, segment.Length );
        }


        public void Set( IEnumerable<Segment> segments )
        {
            foreach( var segment in segments ) Set( segment );
        }


        public void Set( NaiveRanges other )
        {
            if( data.Length != other.data.Length )
            {
                throw new InvalidOperationException( $"Size mismatch: {data.Length} vs {other.data.Length}" );
            }

            for( var i = 0; i < data.Length; ++i )
            {
                data[i] |= other.data[i];
            }
        }


        public NaiveRanges MaterialNonimplication( NaiveRanges other )
        {
            if( data.Length != other.data.Length )
            {
                throw new InvalidOperationException( $"Size mismatch: {data.Length} vs {other.data.Length}" );
            }

            var result = new NaiveRanges( data.Length );

            for( var i = 0; i < data.Length; ++i )
            {
                result.data[i] = data[i] & !other.data[i];
            }

            return result;
        }


        public NaiveRanges ConverseNonimplication( NaiveRanges other )
        {
            if( data.Length != other.data.Length )
            {
                throw new InvalidOperationException( $"Size mismatch: {data.Length} vs {other.data.Length}" );
            }

            var result = new NaiveRanges( data.Length );

            for( var i = 0; i < data.Length; ++i )
            {
                result.data[i] = !data[i] & other.data[i];
            }

            return result;
        }


        static public (NaiveRanges leftNoRight, NaiveRanges both, NaiveRanges rightNoLeft) Combine( NaiveRanges left, NaiveRanges right )
        {
            if( left.data.Length != right.data.Length )
            {
                throw new InvalidOperationException( $"Size mismatch: {left.data.Length} vs {right.data.Length}" );
            }

            var left_no_right = new NaiveRanges( left.data.Length );
            var both = new NaiveRanges( left.data.Length );
            var right_no_left = new NaiveRanges( left.data.Length );

            for( var i = 0; i < left.data.Length; ++i )
            {
                left_no_right.data[i] = left.data[i] && !right.data[i];
                both.data[i] = left.data[i] && right.data[i];
                right_no_left.data[i] = !left.data[i] && right.data[i];
            }

            return (left_no_right, both, right_no_left);
        }


        public IEnumerable<Segment> GetSegments( CancellationToken ct, bool valuesToInclude )
        {
            for( int i = 0; ; )
            {
                ct.ThrowIfCancellationRequested( );
                while( i < data.Length && data[i] != valuesToInclude ) ++i;

                if( i >= data.Length ) break;

                int start = i;

                ct.ThrowIfCancellationRequested( );
                while( i < data.Length && data[i] == valuesToInclude ) ++i;

                int length = i - start;

                ct.ThrowIfCancellationRequested( );
                yield return new Segment( start, length );
            }
        }
    }
}
