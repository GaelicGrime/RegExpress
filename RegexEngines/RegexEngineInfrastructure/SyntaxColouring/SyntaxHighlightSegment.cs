using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure.SyntaxColouring
{
	public class SyntaxHighlightSegment
	{
		public int Start { get; }

		public int Length { get; }

		public SyntaxHighlightCategoryEnum Category { get; }


		public SyntaxHighlightSegment( int start, int length, SyntaxHighlightCategoryEnum category )
		{
			Start = start;
			Length = length;
			Category = category;
		}
	}
}
