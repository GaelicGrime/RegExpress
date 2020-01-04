using RegexEngineInfrastructure.Matches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace DotNetRegexEngine.Matches
{
	class DotNetRegexMatch : RegexMatch
	{
		readonly Match Match;

		public DotNetRegexMatch( Match match )
		{
			Match = match;
		}

		public override int Index => Match.Index;

		public override int Length => Match.Length;

		public override string Value => Match.Value;

		public override bool Success => Match.Success;

		public override string Name => Match.Name;

		public override IEnumerable<RegexCapture> Captures
		{
			get
			{
				return Match.Captures.OfType<Capture>( ).Select( c => new DotNetRegexCapture( c ) );
			}
		}

		public override IEnumerable<RegexGroup> Groups
		{
			get
			{
				return Match.Groups.OfType<Group>( ).Select( g => new DotNetRegexGroup( g ) );
			}
		}

		public override string ToString( ) => Match.ToString( );
	}
}
