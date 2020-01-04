using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure.Matches
{
	public abstract class RegexCapture
	{
		public abstract int Index { get; }

		public abstract int Length { get; }

		public abstract string Value { get; }
	}

}
