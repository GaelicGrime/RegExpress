using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.SyntaxColouring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CppPcre2RegexEngineNs
{
	public class CppPcre2RegexEngine : IRegexEngine
	{
		readonly UCCppPcre2RegexOptions OptionsControl;

		public CppPcre2RegexEngine( )
		{
			OptionsControl = new UCCppPcre2RegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;

		}


		#region IRegexEngine

		public string Id => "CppPcre2Regex";

		public string Name => "C++ PCRE2";

		public string EngineVersion => "//..."; // CppBoostRegexInterop.CppMatcher.GetBoostVersion( );

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
			throw new NotImplementedException( );

			//.........
			//var selected_options = OptionsControl.CachedOptions;

			//return new CppBoostRegexInterop.CppMatcher( pattern, selected_options );

			//return null;
		}


		public void ColourisePattern( ICancellable cnc, ColouredSegments colouredSegments, string pattern, Segment visibleSegment )
		{
			//...
		}


		public void HighlightPattern( ICancellable cnc, Highlights highlights, string pattern, int selectionStart, int selectionEnd, Segment visibleSegment )
		{
			//...
		}

		#endregion IRegexEngine


		private void OptionsControl_Changed( object sender, EventArgs e )
		{
			OptionsChanged?.Invoke( this, null );
		}

	}
}
