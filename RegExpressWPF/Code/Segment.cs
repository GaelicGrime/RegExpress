using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegExpressWPF.Code
{
    public struct Segment
    {
        public readonly int Index;
        public readonly int Length;


        public Segment( int index, int length )
        {
            Index = index;
            Length = length;
        }
    }
}
