using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure.SyntaxColouring
{
	public class ColouredSegments
	{
		public List<Segment> Comments { get; } = new List<Segment>( );
		public List<Segment> Escapes { get; } = new List<Segment>( );
		public List<Segment> GroupNames { get; } = new List<Segment>( );

		public IEnumerable<List<Segment>> All { get; }


		public ColouredSegments( )
		{
			All = new List<List<Segment>> { Comments, Escapes, GroupNames };
		}
	}
}
