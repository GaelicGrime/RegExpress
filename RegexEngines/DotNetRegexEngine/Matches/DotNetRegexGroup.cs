using RegexEngineInfrastructure.Matches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace DotNetRegexEngine.Matches
{
	class DotNetRegexGroup : RegexGroup
	{
		readonly Group Group;

		public DotNetRegexGroup( Group group )
		{
			Group = group;
		}

		public override int Index => Group.Index;

		public override int Length => Group.Length;

		public override string Value => Group.Value;

		public override bool Success => Group.Success;

		public override string Name => Group.Name;

		public override IEnumerable<RegexCapture> Captures
		{
			get
			{
				return Group.Captures.OfType<Capture>( ).Select( c => new DotNetRegexCapture( c ) );
			}
		}

		public override string ToString( ) => Group.ToString( );
	}
}
