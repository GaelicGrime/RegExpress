using DotNetRegexEngine.Matches;
using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace DotNetRegexEngine
{
	public class DotNetRegexEngine : IRegexEngine
	{

		public string[] ParseLegacyOptions( int flags )
		{
			RegexOptions dotnet_options = (RegexOptions)flags;

			var list = new List<string>( );

			foreach( DotNetRegexOptionInfo o in AllOptions )
			{
				if( ( dotnet_options & o.RegexOption ) != 0 )
				{
					list.Add( o.AsText );
				}
			}

			return list.ToArray( );
		}


		#region IRegexEngine

		public string Id => "DotNetRegex";


		public IReadOnlyCollection<IRegexOptionInfo> AllOptions
		{
			get
			{
				return new List<IRegexOptionInfo>
				{
					MakeOptionInfo( RegexOptions.CultureInvariant ),
					MakeOptionInfo( RegexOptions.ECMAScript ),
					MakeOptionInfo( RegexOptions.ExplicitCapture ),
					MakeOptionInfo( RegexOptions.IgnoreCase ),
					MakeOptionInfo( RegexOptions.IgnorePatternWhitespace ),
					MakeOptionInfo( RegexOptions.Multiline, "('^', '$' at '\\n' too)" ),
					MakeOptionInfo( RegexOptions.RightToLeft ),
					MakeOptionInfo( RegexOptions.Singleline, "('.' matches '\\n' too)" ),
				};
			}
		}


		public IParsedPattern ParsePattern( string pattern, IReadOnlyCollection<IRegexOptionInfo> options )
		{
			RegexOptions regex_options = RegexOptions.None;

			foreach( DotNetRegexOptionInfo opt in options )
			{
				regex_options |= opt.RegexOption;
			}

			var regex = new Regex( pattern, regex_options );

			return new DotNetParsedPattern( regex );
		}


		#endregion IRegexEngine


		IRegexOptionInfo MakeOptionInfo( RegexOptions option, string note = null )
		{
			return new DotNetRegexOptionInfo( option, note );
		}
	}
}
