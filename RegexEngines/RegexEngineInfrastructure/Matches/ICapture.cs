using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure.Matches
{
	public interface ICapture
	{
		int Index { get; }

		int Length { get; }

		string Value { get; }
	}

}
