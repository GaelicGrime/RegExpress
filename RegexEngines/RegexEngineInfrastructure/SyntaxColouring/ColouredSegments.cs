using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure.SyntaxColouring
{
	public class ColouredSegments
	{
		public List<Segment> Comments { get; }

		public IEnumerable<List<Segment>> All { get; }


		public ColouredSegments( )
		{
			Comments = new List<Segment>( );

			All = new List<List<Segment>> { Comments };
		}
	}
}
