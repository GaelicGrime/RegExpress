using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.SyntaxColouring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;


namespace RegexEngineInfrastructure
{
	public interface IRegexEngine
	{
		string Id { get; }

		string Name { get; }

		string EngineVersion { get; }

		event EventHandler OptionsChanged;

		Control GetOptionsControl( );

		object SerializeOptions( );

		void DeserializeOptions( object obj );

		IMatcher ParsePattern( string pattern );

		void ColourisePattern( ICancellable cnc, ColouredSegments colouredSegments, string pattern, Segment visibleSegment );

		Highlights HighlightPattern( ICancellable cnc, string pattern, int selectionStart, int selectionEnd, Segment visibleSegment );
	}
}
