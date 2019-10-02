using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace RegExpressWPF.Code
{
	public sealed class TabData
	{
#pragma warning disable CA1051 // Do not declare visible instance fields

		public string Name;
		public string Pattern;
		public string Text;
		public RegexOptions RegexOptions;
		public bool ShowFirstMatchOnly;
		public bool ShowCaptures;
		public bool ShowWhiteSpaces;
		public string Eol;

#pragma warning restore CA1051 // Do not declare visible instance fields
	}
}
