using RegexEngineInfrastructure.Matches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace DotNetRegexEngine.Matches
{
	class DotNetRegexCapture : RegexCapture
	{
		readonly Capture Capture;

		public DotNetRegexCapture( Capture capture )
		{
			Capture = capture;
		}

		public override int Index => Capture.Index;

		public override int Length => Capture.Length;

		public override string Value => Capture.Value;

		public override string ToString( ) => Capture.ToString( );
	}
}
