using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.SyntaxColouring;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;


namespace DotNetCoreRegexEngineNs
{
	public class DotNetCoreRegexEngine : IRegexEngine
	{
		readonly UCDotNetCoreRegexOptions OptionsControl;
		static readonly Lazy<string> LazyVersion = new Lazy<string>( GetVersion );

		static readonly Dictionary<string, Regex> CachedColouringRegexes = new Dictionary<string, Regex>( );
		static readonly Dictionary<string, Regex> CachedHighlightingRegexes = new Dictionary<string, Regex>( );


		static DotNetCoreRegexEngine( )
		{
		}

		public DotNetCoreRegexEngine( )
		{
			OptionsControl = new UCDotNetCoreRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "DotNetCoreRegex";

		public string Name => "Regex, .NET";

		public string EngineVersion => LazyVersion.Value;

		public RegexEngineCapabilityEnum Capabilities => RegexEngineCapabilityEnum.Default;

		public string NoteForCaptures => null;

		public event RegexEngineOptionsChanged OptionsChanged;


		public Control GetOptionsControl( )
		{
			return OptionsControl;
		}


		public string[] ExportOptions( )
		{
			DotNetCoreRegexOptions options = OptionsControl.GetSelectedOptions( );
			var json = JsonSerializer.Serialize( options );

			return new[] { $"json:{json}" };
		}


		public void ImportOptions( string[] options )
		{
			var json = options.FirstOrDefault( o => o.StartsWith( "json:" ) )?.Substring( "json:".Length );

			DotNetCoreRegexOptions options_obj;
			if( string.IsNullOrWhiteSpace( json ) )
			{
				options_obj = new DotNetCoreRegexOptions( );
			}
			else
			{
				options_obj = JsonSerializer.Deserialize<DotNetCoreRegexOptions>( json );
			}

			OptionsControl.SetSelectedOptions( options_obj );
		}


		public IMatcher ParsePattern( string pattern )
		{
			DotNetCoreRegexOptions options = OptionsControl.GetSelectedOptions( );

			return new DotNetCoreMatcher( pattern, options );
		}


		public void ColourisePattern( ICancellable cnc, ColouredSegments colouredSegments, string pattern, Segment visibleSegment )
		{
			Regex regex = GetCachedColouringRegex( OptionsControl.GetSelectedOptions( ) );

			foreach( Match m in regex.Matches( pattern ) )
			{
				Debug.Assert( m.Success );

				if( cnc.IsCancellationRequested ) return;

				// comments, '(?#...)' and '#...'
				{
					var g = m.Groups["comment"];
					if( g.Success )
					{
						if( cnc.IsCancellationRequested ) return;

						var intersection = Segment.Intersection( visibleSegment, g.Index, g.Length );

						if( !intersection.IsEmpty )
						{
							colouredSegments.Comments.Add( intersection );
						}

						continue;
					}
				}

				if( cnc.IsCancellationRequested ) return;

				// escapes, '\...'
				{
					var g = m.Groups["escape"];
					if( g.Success )
					{
						if( cnc.IsCancellationRequested ) return;

						// we need captures because of '*?'
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

				// named groups, '(?<name>...' and "(?'name'...", including balancing groups
				{
					var g = m.Groups["name"];
					if( g.Success )
					{
						if( cnc.IsCancellationRequested ) return;

						var intersection = Segment.Intersection( visibleSegment, g.Index, g.Length );

						if( !intersection.IsEmpty )
						{
							colouredSegments.GroupNames.Add( intersection );
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

			Regex regex = GetCachedHighlightingRegex( OptionsControl.GetSelectedOptions( ) );

			HighlightHelper.CommonHighlighting( cnc, highlights, pattern, selectionStart, selectionEnd, visibleSegment, regex, par_size, bracket_size );
		}

		#endregion IRegexEngine


		private void OptionsControl_Changed( object sender, RegexEngineOptionsChangedArgs args )
		{
			OptionsChanged?.Invoke( this, args );
		}


		static Regex GetCachedColouringRegex( DotNetCoreRegexOptions options )
		{
			string key = options.IgnorePatternWhitespace.ToString( );

			lock( CachedColouringRegexes )
			{
				if( CachedColouringRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateCachedColouringRegex( options );

				CachedColouringRegexes.Add( key, regex );

				return regex;
			}
		}


		static Regex GetCachedHighlightingRegex( DotNetCoreRegexOptions options )
		{
			string key = options.IgnorePatternWhitespace.ToString( );

			lock( CachedHighlightingRegexes )
			{
				if( CachedHighlightingRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateHighlightingRegex( options );

				CachedHighlightingRegexes.Add( key, regex );

				return regex;
			}
		}


		static Regex CreateCachedColouringRegex( DotNetCoreRegexOptions options )
		{
			// (some patterns includes incomplete constructs)

			var pb = new PatternBuilder( );

			pb.BeginGroup( "comment" );
			pb.Add( @"\(\?\#.*?(\)|$)" );
			if( options.IgnorePatternWhitespace ) pb.Add( @"\#[^\n]*" );
			pb.EndGroup( );

			var escapes_pb = new PatternBuilder( );

			escapes_pb.BeginGroup( "escape" );
			escapes_pb.Add( @"\\[0-7]{2,3}" );
			escapes_pb.Add( @"\\x[0-9A-Fa-f]{1,2}" );
			escapes_pb.Add( @"\\c[A-Za-z]" );
			escapes_pb.Add( @"\\u[0-9A-Fa-f]{1,4}" );
			escapes_pb.Add( @"\\(p|P)\{.*?(\}|$)" );
			escapes_pb.Add( @"\\k<([A-Za-z]+>)?" );
			escapes_pb.Add( @"\\." );
			escapes_pb.EndGroup( );

			pb.AddGroup( null, $@"\[\]?({escapes_pb.ToPattern( )} |.)*?(\]|$)" );

			pb.Add( @"\(\?(?'name'<(?![=!]).*?(>|$))" ); // (balancing groups covered too)
			pb.Add( @"\(\?(?'name''.*?('|$))" );
			pb.Add( @"(?'name'\\k<.*?(>|$))" );
			pb.Add( @"(?'name'\\k'.*?('|$))" );

			pb.Add( escapes_pb.ToPattern( ) );

			var regex = pb.ToRegex( );

			return regex;
		}


		static Regex CreateHighlightingRegex( DotNetCoreRegexOptions options )
		{
			var pb = new PatternBuilder( );

			pb.Add( @"\(\?\#.*?(\)|$)" ); // comment
			if( options.IgnorePatternWhitespace ) pb.Add( @"\#[^\n]*" ); // line comment
			pb.Add( @"\\[pP]\{.*?(\}|$)" ); // (skip)
			pb.Add( @"(?'left_par'\()" ); // '('
			pb.Add( @"(?'right_par'\))" ); // ')'
			pb.Add( @"(?'left_brace'\{) \d+(,(\d+)?)? ((?'right_brace'\})|$)" ); // '{...}'
			pb.Add( @"(?'left_bracket'\[) \]? (\\.|.)*? ((?'right_bracket'\])|$)" ); // '[...]'
			pb.Add( @"\\." ); // (skip)

			return pb.ToRegex( );
		}


		static string GetVersion( )
		{
			try
			{
				return DotNetCoreMatcher.GetVersion( );
			}
			catch( Exception exc )
			{
				_ = exc;
				if( Debugger.IsAttached ) Debugger.Break( );

				return null;
			}
		}

	}
}
