using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure
{
	public sealed class SurrogatePairsHelper
	{
		readonly List<int> SurrogatePairs = new List<int>( );

		public SurrogatePairsHelper( string text )
		{
			CollectSurrogatePairs( text );
		}


		public int GetAlternativeIndex(int index)
		{
			throw new NotImplementedException( );
		}


		void CollectSurrogatePairs( string text )
		{
			SurrogatePairs.Clear( );

			for( int i = 0; i < text.Length; )
			{
				if( char.IsSurrogatePair( text, i ) )
				{
					Debug.Assert( i <= text.Length - 2 );
					if( i <= text.Length - 2 )
					{
						SurrogatePairs.Add( i );
					}
					i += 2;
				}
				else
				{
					++i;
				}
			}
		}
	}
}
