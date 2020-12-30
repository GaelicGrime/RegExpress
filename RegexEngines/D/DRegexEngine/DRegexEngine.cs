using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.SyntaxColouring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace DRegexEngineNs
{
	public class DRegexEngine : IRegexEngine
	{
		readonly UCDRegexOptions OptionsControl;
		static readonly object DVersionLocker = new object( );
		static string DVersion = null;

		public DRegexEngine( )
		{
			OptionsControl = new UCDRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "DRegex";

		public string Name => "D";


		public string EngineVersion
		{
			get
			{
				if( DVersion == null )
				{
					lock( DVersionLocker )
					{
						if( DVersion == null )
						{
							try
							{
								DVersion = DMatcher.GetDVersion( NonCancellable.Instance );
							}
							catch
							{
								DVersion = "Unknown Version";
							}
						}
					}
				}

				return DVersion;
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
			DRegexOptions options = OptionsControl.GetSelectedOptions( );
			var json = JsonSerializer.Serialize( options );

			return new[] { $"json:{json}" };
		}


		public void ImportOptions( string[] options )
		{
			var json = options.FirstOrDefault( o => o.StartsWith( "json:" ) )?.Substring( "json:".Length );

			DRegexOptions options_obj;
			if( string.IsNullOrWhiteSpace( json ) )
			{
				options_obj = new DRegexOptions( );
			}
			else
			{
				options_obj = JsonSerializer.Deserialize<DRegexOptions>( json );
			}

			OptionsControl.SetSelectedOptions( options_obj );
		}


		public IMatcher ParsePattern( string pattern )
		{
			DRegexOptions options = OptionsControl.GetSelectedOptions( );

			return new DMatcher( pattern, options );
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
