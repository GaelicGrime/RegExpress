using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure.Matches.Simple
{
	public interface ISimpleTextGetter
	{
		string GetText( int index, int length );
	}
}
