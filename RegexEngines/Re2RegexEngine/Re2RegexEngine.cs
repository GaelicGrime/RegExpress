using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.SyntaxColouring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Re2RegexEngineNs
{
	public class Re2RegexEngine : IRegexEngine
	{
		readonly UCRe2RegexOptions OptionsControl;


		public Re2RegexEngine( )
		{
			OptionsControl = new UCRe2RegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;

		}


		#region IRegexEngine

		public string Id => "CppRe2Regex";

		public string Name => "RE2";

		public string EngineVersion => Re2RegexInterop.Matcher.GetRe2Version( );

		public RegexEngineCapabilityEnum Capabilities => RegexEngineCapabilityEnum.NoCaptures;

		public event EventHandler OptionsChanged;


		public Control GetOptionsControl( )
		{
			return OptionsControl;
		}


		public object SerializeOptions( )
		{
			return OptionsControl.ToSerialisableObject( );
		}


		public void DeserializeOptions( object obj )
		{
			OptionsControl.FromSerializableObject( obj );
		}


		public IMatcher ParsePattern( string pattern )
		{
			string[] selected_options = OptionsControl.CachedOptions;

			return new Re2RegexInterop.Matcher( pattern, selected_options );
		}


		public void ColourisePattern( ICancellable cnc, ColouredSegments colouredSegments, string pattern, Segment visibleSegment )
		{
		}


		public void HighlightPattern( ICancellable cnc, Highlights highlights, string pattern, int selectionStart, int selectionEnd, Segment visibleSegment )
		{
		}

		#endregion


		private void OptionsControl_Changed( object sender, EventArgs e )
		{
			OptionsChanged?.Invoke( this, null );
		}


	}
}
