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

		event EventHandler OptionsChanged;

		Control GetOptionsControl( );

		object SerializeOptions( );

		void DeserializeOptions( object obj );

		IMatcher ParsePattern( string pattern );

		void ColourisePattern( ICancellable cnc, IColouriser colouriser, string pattern, Segment visibleSegment );
	}
}
