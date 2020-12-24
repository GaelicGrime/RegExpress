using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace RegExpressWPF.Code
{
	public sealed class TabData
	{
		public string Name;
		public string Pattern;
		public string Text;
		public string ActiveRegexEngineId;
		public Dictionary<string /*Engine ID*/, string[] /* options */> AllRegexOptions = new Dictionary<string, string[]>( );
		public bool ShowFirstMatchOnly;
		public bool ShowSucceededGroupsOnly;
		public bool ShowCaptures;
		public bool ShowWhiteSpaces;
		public string Eol;
	}


	public class TabMetrics
	{
		public double
			RightColumnWidth,
			TopRowHeight,
			BottomRowHeight;
	}
}
