using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure.SyntaxColouring
{
	public interface IColouriser
	{
		void Colourise( Segment segment, SyntaxHighlightCategoryEnum category );
	}
}
