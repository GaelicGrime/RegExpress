using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.SyntaxColouring;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;


namespace SubRegRegexEngineNs
{
	public class SubRegRegexEngine : IRegexEngine
	{
		readonly UCSubRegRegexOptions OptionsControl;

		public SubRegRegexEngine( )
		{
			OptionsControl = new UCSubRegRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "SubReg";

		public string Name => "SubReg";

		public string EngineVersion => "04-01-2020";

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

			return new SubRegRegexInterop.Matcher( pattern, selected_options );
		}


		public void ColourisePattern( ICancellable cnc, ColouredSegments colouredSegments, string pattern, Segment visibleSegment )
		{
			Regex regex = ColouringRegex;

			foreach( Match m in regex.Matches( pattern ) )
			{
				Debug.Assert( m.Success );

				if( cnc.IsCancellationRequested ) return;

				// escapes, '\...'
				{
					var g = m.Groups["escape"];
					if( g.Success )
					{
						if( cnc.IsCancellationRequested ) return;

						foreach( Capture c in g.Captures )
						{
							if( cnc.IsCancellationRequested ) return;

							var intersection = Segment.Intersection( visibleSegment, c.Index, c.Length );

							if( !intersection.IsEmpty )
							{
								colouredSegments.Escapes.Add( intersection );
							}
						}
					}
				}

				if( cnc.IsCancellationRequested ) return;
			}
		}


		public void HighlightPattern( ICancellable cnc, Highlights highlights, string pattern, int selectionStart, int selectionEnd, Segment visibleSegment )
		{
			int par_size = 1;
			int bracket_size = 1;

			Regex regex = HighlightingRegex;

			HighlightHelper.CommonHighlighting( cnc, highlights, pattern, selectionStart, selectionEnd, visibleSegment, regex, par_size, bracket_size );
		}

		#endregion

		private void OptionsControl_Changed( object sender, RegexEngineOptionsChangedArgs args )
		{
			OptionsChanged?.Invoke( this, args );
		}


		static Regex CreateColouringRegex( )
		{
			var pb = new PatternBuilder( );

			pb
				.BeginGroup( "escape" )
				.Add( @"\\x[0-9a-fA-F]+" )
				.Add( @"\\." )
				.EndGroup( );

			return pb.ToRegex( );
		}


		static Regex CreateHighlightingRegex( )
		{
			var pb = new PatternBuilder( );

			pb.Add( @"\\." );
			pb.AddGroup( "left_par", @"(?'left_par'\()" ); // '('
			pb.AddGroup( "right_par", @"(?'right_par'\))" ); // ')'

			return pb.ToRegex( );
		}


		static readonly Regex ColouringRegex = CreateColouringRegex( );
		static readonly Regex HighlightingRegex = CreateHighlightingRegex( );
	}
}
