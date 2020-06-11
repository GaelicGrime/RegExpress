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
	[KnownType( typeof( RegexOptions ) )]
	[KnownType( typeof( string[] ) )]
	public sealed class TabData
	{
#pragma warning disable CA1051 // Do not declare visible instance fields

		public string Name;
		public string Pattern;
		public string Text;
		public string RegexEngineId;
		public object RegexOptions; // TODO: consider a single Dictionary for active and inactive engines
		public Dictionary<string, string[]> InactiveRegexOptions;
		public bool ShowFirstMatchOnly;
		public bool ShowSucceededGroupsOnly;
		public bool ShowCaptures;
		public bool ShowWhiteSpaces;
		public string Eol;

#pragma warning restore CA1051 // Do not declare visible instance fields
	}


	public class TabMetrics
	{
#pragma warning disable CA1051 // Do not declare visible instance fields

		public double
			RightColumnWidth,
			TopRowHeight,
			BottomRowHeight;

#pragma warning restore CA1051 // Do not declare visible instance fields

	}
}
