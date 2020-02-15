﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure.SyntaxColouring
{
	public class Highlights
	{
		// (Positions in the text; empty if no highlights)

		public Segment LeftPar = Segment.Empty;
		public Segment RightPar = Segment.Empty;

		public Segment LeftBracket = Segment.Empty;
		public Segment RightBracket = Segment.Empty;

		public Segment LeftCurlyBracket = Segment.Empty;
		public Segment RightCurlyBracket = Segment.Empty;
	}
}
