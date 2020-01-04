using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure
{
	public abstract class RegexOptionInfo
	{
		public abstract string Text { get; }
		public abstract string Note { get; }

		public abstract string AsText { get; }
	}
}
