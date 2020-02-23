using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.SyntaxColouring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;


namespace IcuRegexEngineNs
{
	public class IcuRegexEngine : IRegexEngine
	{
		readonly UCIcuRegexOptions OptionsControl;


		public IcuRegexEngine( )
		{
			OptionsControl = new UCIcuRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "IcuRegex";

		public string Name => "Icu";

		public string EngineVersion => IcuRegexInterop.Matcher.GetVersion( );

		public RegexEngineCapabilityEnum Capabilities => RegexEngineCapabilityEnum.Default;

		public string NoteForCaptures => null;


		public event RegexEngineOptionsChanged OptionsChanged;


		public Control GetOptionsControl( )
		{
			return OptionsControl;
		}


		public string[] ExportOptions( )
		{
			return OptionsControl.ExportOptions( );
		}


		public void ImportOptions( string[] options )
		{
			OptionsControl.ImportOptions( options );
		}


		public IMatcher ParsePattern( string pattern )
		{
			string[] selected_options = OptionsControl.CachedOptions;

			throw new NotImplementedException( );

		}


		public void ColourisePattern( ICancellable cnc, ColouredSegments colouredSegments, string pattern, Segment visibleSegment )
		{

		}

		public void HighlightPattern( ICancellable cnc, Highlights highlights, string pattern, int selectionStart, int selectionEnd, Segment visibleSegment )
		{

		}

		#endregion


		private void OptionsControl_Changed( object sender, RegexEngineOptionsChangedArgs args )
		{
			OptionsChanged?.Invoke( this, args );
		}





		static readonly Regex EndGroupRegex = new Regex( @"(\s*\|\s*)?$", RegexOptions.ExplicitCapture | RegexOptions.Compiled );

		static string EndGroup( string s, string name )
		{
			if( string.IsNullOrWhiteSpace( s ) ) return null;

			if( name != null )
			{
				s = "(?'" + name + "'" + EndGroupRegex.Replace( s, ")", 1 );
			}
			else
			{
				s = "(" + EndGroupRegex.Replace( s, ")", 1 );
			}

			return s;
		}

	}
}
