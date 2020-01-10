using RegexEngineInfrastructure;
using System.Text.RegularExpressions;


namespace DotNetRegexEngine
{
	class DotNetRegexOptionInfo : IRegexOptionInfo
	{
		public DotNetRegexOptionInfo( RegexOptions option, string note )
		{
			Note = note;
			RegexOption = option;
		}


		public RegexOptions RegexOption { get; }


		#region IRegexOptionInfo

		public string Text => RegexOption.ToString( );

		public string Note { get; }

		public string GroupName => null;

		public string AsText => RegexOption.ToString( );

		#endregion IRegexOptionInfo
	}
}
