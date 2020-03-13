using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure.Matches.Simple
{
	public abstract class SimpleBase
	{
		protected readonly ISimpleTextGetter TextGetter;


		protected SimpleBase( int index, int length, ISimpleTextGetter textGetter )
		{
			Index = index;
			Length = length;
			TextGetter = textGetter;
		}


		public int Index { get; }

		public int Length { get; }

		public string Value => TextGetter.GetText( Index, Length );
	}
}
