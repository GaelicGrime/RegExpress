﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure.SyntaxColouring
{
	public class Highlights
	{
		// (Positions in the text; negative if no highlights)

		public Segment LeftPara = Segment.Empty;
		public Segment RightPara = Segment.Empty;

		public Segment LeftBracket = Segment.Empty;
		public Segment RightBracket = Segment.Empty;

	}
}
