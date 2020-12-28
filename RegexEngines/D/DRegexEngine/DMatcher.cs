using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.Matches.Simple;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DRegexEngineNs
{
	sealed class DMatcher : IMatcher, ISimpleTextGetter
	{
		readonly DRegexOptions Options;
		readonly string Pattern;
		string Text;


		public DMatcher( string pattern, DRegexOptions options )
		{
			Options = options;
			Pattern = pattern;
		}


		#region IMatcher

		public RegexMatches Matches( string text, ICancellable cnc )
		{
			Text = text; //......

			return RegexMatches.Empty;
		}

		#endregion IMatcher


		#region ISimpleTextGetter

		public string GetText( int index, int length )
		{
			return Text.Substring( index, length );
		}

		#endregion ISimpleTextGetter




	}
}
