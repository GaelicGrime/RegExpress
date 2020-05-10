using RegexEngineInfrastructure.Matches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace DotNetRegexEngineNs.Matches
{
	class DotNetRegexCapture : ICapture
	{
		readonly Capture Capture;

		public DotNetRegexCapture( Capture capture )
		{
			Capture = capture;
		}


		#region ICapture

		public int Index => Capture.Index;

		public int Length => Capture.Length;

		public int TextIndex => Capture.Index;

		public int TextLength => Capture.Length;

		public string Value => Capture.Value;

		#endregion ICapture


		public override string ToString( ) => Capture.ToString( );
	}
}
