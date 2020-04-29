using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.SyntaxColouring;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Controls;


namespace PythonRegexEngineNs
{
	public class PythonRegexEngine : IRegexEngine
	{
		readonly UCPythonRegexOptions OptionsControl;

		static readonly Dictionary<string, Regex> CachedColouringRegexes = new Dictionary<string, Regex>( );
		static readonly Dictionary<string, Regex> CachedHighlightingRegexes = new Dictionary<string, Regex>( );


		[DllImport( "kernel32", CharSet = CharSet.Unicode, SetLastError = true )]
		static extern bool SetDllDirectory( string lpPathName );


		static PythonRegexEngine( )
		{
			Assembly current_assembly = Assembly.GetExecutingAssembly( );
			string current_assembly_path = Path.GetDirectoryName( current_assembly.Location );
			string dll_path = Path.Combine( current_assembly_path, @"Python-embed" );

			bool b = SetDllDirectory( dll_path );
			if( !b ) throw new ApplicationException( $"SetDllDirectory failed: '{dll_path}'" );
		}

		public PythonRegexEngine( )
		{
			OptionsControl = new UCPythonRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "Python";

		public string Name => "Python";

		public string EngineVersion => Matcher.GetPythonVersion( );

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

			return new Matcher( pattern, selected_options );
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
