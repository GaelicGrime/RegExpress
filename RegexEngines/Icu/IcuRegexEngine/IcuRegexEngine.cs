using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.SyntaxColouring;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;


namespace IcuRegexEngineNs
{
	public class IcuRegexEngine : IRegexEngine
	{
		readonly UCIcuRegexOptions OptionsControl;

		static readonly Dictionary<object, Regex> CachedColouringRegexes = new Dictionary<object, Regex>( );
		static readonly Dictionary<object, Regex> CachedHighlightingRegexes = new Dictionary<object, Regex>( );
		static readonly Regex EmptyRegex = new Regex( "(?!)", RegexOptions.Compiled | RegexOptions.ExplicitCapture );


		[DllImport( "kernel32", CharSet = CharSet.Unicode, SetLastError = true )]
		static extern bool SetDllDirectory( string lpPathName );


		static IcuRegexEngine( )
		{
			Assembly current_assembly = Assembly.GetExecutingAssembly( );
			string current_assembly_path = Path.GetDirectoryName( current_assembly.Location );
			string dll_path = Path.Combine( current_assembly_path, @"ICU-min\bin64" );

			bool b = SetDllDirectory( dll_path );
			if( !b ) throw new ApplicationException( $"SetDllDirectory failed: '{dll_path}'" );
		}


		public IcuRegexEngine( )
		{
			OptionsControl = new UCIcuRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "IcuRegex";

		public string Name => "ICU";

		public string EngineVersion => IcuRegexInterop.Matcher.GetVersion( );

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

			return new IcuRegexInterop.Matcher( pattern, selected_options );
		}


		public void ColourisePattern( ICancellable cnc, ColouredSegments colouredSegments, string pattern, Segment visibleSegment )
		{
			Regex regex = GetCachedColouringRegex( );

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

						continue;
					}
				}

				if( cnc.IsCancellationRequested ) return;

				// comments, '(?#...)', '#...'
				{
					var g = m.Groups["comment"];
					if( g.Success )
					{
						if( cnc.IsCancellationRequested ) return;

						foreach( Capture c in g.Captures )
						{
							if( cnc.IsCancellationRequested ) return;

							var intersection = Segment.Intersection( visibleSegment, c.Index, c.Length );

							if( !intersection.IsEmpty )
							{
								colouredSegments.Comments.Add( intersection );
							}
						}

						continue;
					}
				}

				if( cnc.IsCancellationRequested ) return;

				// named groups and back references
				{
					var g = m.Groups["name"];
					if( g.Success )
					{
						if( cnc.IsCancellationRequested ) return;

						foreach( Capture c in g.Captures )
						{
							if( cnc.IsCancellationRequested ) return;

							var intersection = Segment.Intersection( visibleSegment, c.Index, c.Length );

							if( !intersection.IsEmpty )
							{
								colouredSegments.GroupNames.Add( intersection );
							}
						}

						continue;
					}
				}
			}
		}


		public void HighlightPattern( ICancellable cnc, Highlights highlights, string pattern, int selectionStart, int selectionEnd, Segment visibleSegment )
		{
			int par_size = 1;
			int bracket_size = 1;

			Regex regex = GetCachedHighlightingRegex( );

			HighlightHelper.CommonHighlighting( cnc, highlights, pattern, selectionStart, selectionEnd, visibleSegment, regex, par_size, bracket_size );
		}

		#endregion


		private void OptionsControl_Changed( object sender, RegexEngineOptionsChangedArgs args )
		{
			OptionsChanged?.Invoke( this, args );
		}


		Regex GetCachedColouringRegex( )
		{
			string[] selected_options = OptionsControl.CachedOptions;

			bool UREGEX_LITERAL = selected_options.Contains( "UREGEX_LITERAL" );
			if( UREGEX_LITERAL ) return EmptyRegex;

			bool UREGEX_COMMENTS = selected_options.Contains( "UREGEX_COMMENTS" );

			object key = UREGEX_COMMENTS;

			lock( CachedColouringRegexes )
			{
				if( CachedColouringRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateColouringRegex( UREGEX_COMMENTS );

				CachedColouringRegexes.Add( key, regex );

				return regex;
			}
		}


		Regex GetCachedHighlightingRegex( )
		{
			string[] selected_options = OptionsControl.CachedOptions;

			bool UREGEX_LITERAL = selected_options.Contains( "UREGEX_LITERAL" );
			if( UREGEX_LITERAL ) return EmptyRegex;

			bool UREGEX_COMMENTS = selected_options.Contains( "UREGEX_COMMENTS" );

			object key = UREGEX_COMMENTS;

			lock( CachedHighlightingRegexes )
			{
				if( CachedHighlightingRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateHighlightingRegex( UREGEX_COMMENTS );

				CachedHighlightingRegexes.Add( key, regex );

				return regex;
			}
		}


		Regex CreateColouringRegex( bool UREGEX_COMMENTS )
		{
			string escape = "";

			escape += @"\\c[A-Za-z] | "; // \cx control char
			escape += @"\\[NpP]\{.*?(\} | $) | "; // named character, property
			escape += @"\\[uUx][0-9a-fA-F]+ | "; // hexadecimal char
			escape += @"\\x\{[0-9a-fA-F]+(\}|$) | "; // hexadecimal char
			escape += @"\\0[0-7]+ | "; // octal
			escape += @"\\. | "; // \.

			escape = RegexUtilities.EndGroup( escape, "escape" );

			//

			string named_group = "";

			named_group += @"\(\?(?'name'<(?![=!]).*?(>|$)) | ";
			named_group += @"(?'name'\\k<.*?(>|$)) | ";

			named_group = RegexUtilities.EndGroup( named_group, "named_group" );

			//

			string quote = "";

			quote = @"\\Q.*?(\\E|$) | "; // quoted part

			quote = RegexUtilities.EndGroup( quote, "escape" ); // use 'escape' name to take its colour

			//

			string char_group = "";
			string posix_bracket = @"(?'escape'\[:.*?(:\]|$)) |"; // [:...:], use escape colour

			char_group = $@"
						\[
						\]?
						(?> {posix_bracket} \[(?<c>) | ({escape} | [^\[\]])+ | \](?<-c>))*
						(?(c)(?!))
						\]
						";

			char_group = RegexUtilities.EndGroup( char_group, null );

			//

			string comment = "";

			comment += @"\(\?\#.*?(\)|$) | "; // comment

			if( UREGEX_COMMENTS ) comment += @"\#.*?(\n|$) | "; // line-comment

			comment = RegexUtilities.EndGroup( comment, "comment" );

			//

			string[] all = new[]
			{
				// (order is important)
				comment,
				quote,
				named_group,
				char_group,
				escape,
			};

			string pattern = @"(?nsx)(" + Environment.NewLine +
				string.Join( " | " + Environment.NewLine, all.Where( s => !string.IsNullOrWhiteSpace( s ) ) ) +
				")";

			var regex = new Regex( pattern, RegexOptions.Compiled | RegexOptions.ExplicitCapture );

			return regex;
		}


		Regex CreateHighlightingRegex( bool UREGEX_COMMENTS )
		{
			string pattern = "";

			pattern += @"\(\?\#.*?(\)|$) | "; // comment

			if( UREGEX_COMMENTS ) pattern += @"\#.*?(\n|$) | "; // line-comment
			pattern += @"\\Q.*?(\\E|$) | "; // quoted part

			pattern += @"(?'left_par'\() | "; // '('
			pattern += @"(?'right_par'\)) | "; // ')'

			pattern += @"\\[NpPx]\{.*?(\}|$) | "; // (skip)

			pattern += @"(?'left_brace'\{).*?((?'right_brace'\})|$) | "; // '{...}'

			string posix_bracket = @"(\[:.*?(:\]|$)) | "; // [:...:]

			pattern += $@"
						(?'left_bracket'\[)
						\]?
						(?> {posix_bracket} (?'left_bracket'\[)(?<c>) | (\\. | [^\[\]])+ | (?'right_bracket'\])(?<-c>))*
						(?(c)(?!))
						(?'right_bracket'\])?
						|
						(?'right_bracket'\])
						| ";

			pattern += @"\\. | "; // '\...'

			pattern = RegexUtilities.EndGroup( pattern, null );

			if( string.IsNullOrWhiteSpace( pattern ) )
				return EmptyRegex;
			else
				pattern = "(?nsx)" + pattern;

			var regex = new Regex( pattern, RegexOptions.Compiled | RegexOptions.ExplicitCapture );

			return regex;
		}

	}
}
