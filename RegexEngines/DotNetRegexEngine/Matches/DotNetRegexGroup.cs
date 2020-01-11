using RegexEngineInfrastructure.Matches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace DotNetRegexEngineNs.Matches
{
	class DotNetRegexGroup : IGroup
	{
		readonly Group Group;

		public DotNetRegexGroup( Group group )
		{
			Group = group;
		}

		#region ICapture

		public int Index => Group.Index;

		public int Length => Group.Length;

		public string Value => Group.Value;

		public bool Success => Group.Success;

		public string Name => Group.Name;

		public IEnumerable<ICapture> Captures
		{
			get
			{
				return Group.Captures.OfType<Capture>( ).Select( c => new DotNetRegexCapture( c ) );
			}
		}

		#endregion ICapture

		public override string ToString( ) => Group.ToString( );
	}
}
