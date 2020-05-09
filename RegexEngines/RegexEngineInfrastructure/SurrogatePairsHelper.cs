using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure
{
	public sealed class SurrogatePairsHelper
	{
		public SurrogatePairsHelper( string text )
		{
			CollectSurrogatePairs( text );
		}


		void CollectSurrogatePairs( string text )
		{
			throw new NotImplementedException( );
		}
	}
}
