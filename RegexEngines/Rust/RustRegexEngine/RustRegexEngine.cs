using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.SyntaxColouring;
using RustRegexEngineNs.Matches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace RustRegexEngineNs
{
	public class RustRegexEngine : IRegexEngine
	{
		readonly UCRustRegexOptions OptionsControl;
		static readonly object RustVersionLocker = new object( );
		static string RustVersion = null;


		public RustRegexEngine( )
		{
			OptionsControl = new UCRustRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "RustRegex";

		public string Name => "Rust Regex";

		public string EngineVersion
		{
			get
			{
				if( RustVersion == null )
				{
					lock( RustVersionLocker )
					{
						if( RustVersion == null )
						{
							try
							{
								RustVersion = RustMatcher.GetRustVersion( NonCancellable.Instance );
							}
							catch
							{
								RustVersion = "Unknown Version";
							}
						}
					}
				}

				return RustVersion;
			}
		}

		public RegexEngineCapabilityEnum Capabilities => RegexEngineCapabilityEnum.NoCaptures;

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

			return new RustMatcher( pattern, selected_options );
		}


		public void ColourisePattern( ICancellable cnc, ColouredSegments colouredSegments, string pattern, Segment visibleSegment )
		{
		}


		public void HighlightPattern( ICancellable cnc, Highlights highlights, string pattern, int selectionStart, int selectionEnd, Segment visibleSegment )
		{
		}

		#endregion IRegexEngine


		private void OptionsControl_Changed( object sender, RegexEngineOptionsChangedArgs args )
		{
			OptionsChanged?.Invoke( this, args );
		}

	}
}
