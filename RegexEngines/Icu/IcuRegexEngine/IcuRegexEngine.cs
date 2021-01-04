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
using System.Text.Json;
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

		public string EngineVersion => IcuMatcher.GetIcuVersion( NonCancellable.Instance );

		public RegexEngineCapabilityEnum Capabilities => RegexEngineCapabilityEnum.NoCaptures;

		public string NoteForCaptures => null;


		public event RegexEngineOptionsChanged OptionsChanged;


		public Control GetOptionsControl( )
		{
			return OptionsControl;
		}


		public string[] ExportOptions( )
		{
			IcuRegexOptions options = OptionsControl.GetSelectedOptions( );
			var json = JsonSerializer.Serialize( options );

			return new[] { $"json:{json}" };
		}


		public void ImportOptions( string[] options )
		{
			var json = options.FirstOrDefault( o => o.StartsWith( "json:" ) )?.Substring( "json:".Length );

			IcuRegexOptions options_obj;
			if( string.IsNullOrWhiteSpace( json ) )
			{
				options_obj = new IcuRegexOptions( );
			}
			else
			{
				options_obj = JsonSerializer.Deserialize<IcuRegexOptions>( json );
			}

			OptionsControl.SetSelectedOptions( options_obj );
		}


		public IMatcher ParsePattern( string pattern )
		{
			IcuRegexOptions options = OptionsControl.GetSelectedOptions( );

			return new IcuMatcher( pattern, options );
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

		#endregion IRegexEngine


		private void OptionsControl_Changed( object sender, RegexEngineOptionsChangedArgs args )
		{
			OptionsChanged?.Invoke( this, args );
		}


		Regex GetCachedColouringRegex( )
		{
			IcuRegexOptions options = OptionsControl.GetSelectedOptions( );

			if( options.UREGEX_LITERAL ) return PatternBuilder.AlwaysFailsRegex;

			object key = options.UREGEX_COMMENTS;

			lock( CachedColouringRegexes )
			{
				if( CachedColouringRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateColouringRegex( options );

				CachedColouringRegexes.Add( key, regex );

				return regex;
			}
		}


		Regex GetCachedHighlightingRegex( )
		{
			IcuRegexOptions options = OptionsControl.GetSelectedOptions( );

			if( options.UREGEX_LITERAL ) return PatternBuilder.AlwaysFailsRegex;

			object key = options.UREGEX_COMMENTS;

			lock( CachedHighlightingRegexes )
			{
				if( CachedHighlightingRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateHighlightingRegex( options );

				CachedHighlightingRegexes.Add( key, regex );

				return regex;
			}
		}


		Regex CreateColouringRegex( IcuRegexOptions options )
		{
			var pb_escape = new PatternBuilder( );

			pb_escape.BeginGroup( "escape" );
			pb_escape.Add( @"\\c[A-Za-z]" ); // \cx control char
			pb_escape.Add( @"\\[NpP]\{.*?(\} | $)" ); // named character, property
			pb_escape.Add( @"\\[uUx][0-9a-fA-F]+" ); // hexadecimal char
			pb_escape.Add( @"\\x\{[0-9a-fA-F]+(\}|$)" ); // hexadecimal char
			pb_escape.Add( @"\\0[0-7]+" ); // octal
			pb_escape.Add( @"\\Q.*?(\\E|$)" ); // quoted part
			pb_escape.Add( @"\\." ); // \.
			pb_escape.EndGroup( );

			var pb = new PatternBuilder( );

			pb.BeginGroup( "comment" );
			pb.Add( @"\(\?\#.*?(\)|$)" ); // comment
			if( options.UREGEX_COMMENTS ) pb.Add( @"\#.*?(\n|$)" ); // line-comment
			pb.EndGroup( );

			pb.Add( @"\(\?(?'name'<(?![=!]).*?(>|$))" );
			pb.Add( @"(?'name'\\k<.*?(>|$))" );

			string posix_bracket = @"(?'escape'\[:.*?(:\]|$))"; // [:...:], use escape colour

			pb.Add( $@"
						\[
						\]?
						(?> {posix_bracket} | \[(?<c>) | ({pb_escape.ToPattern( )} | [^\[\]])+ | \](?<-c>))*
						(?(c)(?!))
						\]
						" );

			pb.Add( pb_escape.ToPattern( ) );

			return pb.ToRegex( );
		}


		Regex CreateHighlightingRegex( IcuRegexOptions options )
		{
			var pb = new PatternBuilder( );

			pb.Add( @"\(\?\#.*?(\)|$)" ); // comment

			if( options.UREGEX_COMMENTS ) pb.Add( @"\#.*?(\n|$)" ); // line-comment
			pb.Add( @"\\Q.*?(\\E|$)" ); // quoted part

			pb.Add( @"(?'left_par'\()" ); // '('
			pb.Add( @"(?'right_par'\))" ); // ')'
			pb.Add( @"\\[NpPx]\{.*?(\}|$)" ); // (skip)
			pb.Add( @"(?'left_brace'\{).*?((?'right_brace'\})|$)" ); // '{...}'

			string posix_bracket = @"(\[:.*?(:\]|$))"; // [:...:]

			pb.Add( $@"
						(?'left_bracket'\[)
						\]?
						(?> {posix_bracket} | (?'left_bracket'\[)(?<c>) | (\\. | [^\[\]])+ | (?'right_bracket'\])(?<-c>))*
						(?(c)(?!))
						(?'right_bracket'\])?
						|
						(?'right_bracket'\])
						" );

			pb.Add( @"\\." ); // '\...'

			return pb.ToRegex( );
		}

	}
}
