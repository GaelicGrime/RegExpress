using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.SyntaxColouring;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace WebView2RegexEngineNs
{
	public class WebView2RegexEngine : IRegexEngine
	{
		readonly UCWebView2RegexOptions OptionsControl;
		static readonly Lazy<string> LazyVersion = new Lazy<string>( GetVersion );

		static readonly Dictionary<object, Regex> CachedColouringRegexes = new Dictionary<object, Regex>( );
		static readonly Dictionary<object, Regex> CachedHighlightingRegexes = new Dictionary<object, Regex>( );

		public WebView2RegexEngine( )
		{
			OptionsControl = new UCWebView2RegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "WebView2Regex";

		public string Name => "WebView2";

		public string EngineVersion => LazyVersion.Value;

		public RegexEngineCapabilityEnum Capabilities => RegexEngineCapabilityEnum.NoCaptures | RegexEngineCapabilityEnum.ScrollErrorsToEnd;

		public string NoteForCaptures => null;


		public event RegexEngineOptionsChanged OptionsChanged;


		public Control GetOptionsControl( )
		{
			return OptionsControl;
		}


		public string[] ExportOptions( )
		{
			WebView2RegexOptions options = OptionsControl.GetSelectedOptions( );
			var json = JsonSerializer.Serialize( options );

			return new[] { $"json:{json}" };
		}


		public void ImportOptions( string[] options )
		{
			var json = options.FirstOrDefault( o => o.StartsWith( "json:" ) )?.Substring( "json:".Length );

			WebView2RegexOptions options_obj;
			if( string.IsNullOrWhiteSpace( json ) )
			{
				options_obj = new WebView2RegexOptions( );
			}
			else
			{
				options_obj = JsonSerializer.Deserialize<WebView2RegexOptions>( json );
			}

			OptionsControl.SetSelectedOptions( options_obj );
		}


		public IMatcher ParsePattern( string pattern )
		{
			WebView2RegexOptions options = OptionsControl.GetSelectedOptions( );

			return new WebView2Matcher( pattern, options );
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

				// named groups, '(?<name>...)'
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
			WebView2RegexOptions options = OptionsControl.GetSelectedOptions( );

			object key = options.u;

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
			WebView2RegexOptions options = OptionsControl.GetSelectedOptions( );

			object key = options.u;

			lock( CachedHighlightingRegexes )
			{
				if( CachedHighlightingRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateHighlightingRegex( options );

				CachedHighlightingRegexes.Add( key, regex );

				return regex;
			}
		}


		Regex CreateColouringRegex( WebView2RegexOptions options )
		{
			var pb_escape = new PatternBuilder( );

			pb_escape.BeginGroup( "escape" );
			pb_escape.Add( @"\\c[A-Za-z]" ); // \cx control char
			pb_escape.Add( @"\\x[0-9a-fA-F]{1,2}" ); // hexadecimal char
			pb_escape.Add( @"\\u[0-9a-fA-F]{1,4}" ); // hexadecimal char

			if( options.u )
			{
				// language=regex
				pb_escape.Add( @"\\u\{[0-9a-fA-F]+(\}|$)" ); // hexadecimal char
															 // language=regex
				pb_escape.Add( @"\\(p|P)\{.*?(\}|$)" ); // unicode property
			}


			pb_escape.Add( @"\\." ); // \.
			pb_escape.EndGroup( );

			var pb = new PatternBuilder( );

			pb.AddGroup( null, $@"\[\]?({pb_escape.ToPattern( )} |.)*?(\]|$)" );

			// language=regex
			pb.Add( @"\(\?(?'name'<(?![=!]).*?(>|$))" );
			// language=regex
			pb.Add( @"(?'name'\\k<.*?(>|$))" );

			pb.Add( pb_escape.ToPattern( ) );

			return pb.ToRegex( );
		}


		Regex CreateHighlightingRegex( WebView2RegexOptions options )
		{
			var pb = new PatternBuilder( );

			pb.Add( @"(?'left_par'\()" ); // '('
			pb.Add( @"(?'right_par'\))" ); // ')'
			pb.Add( @"\\[pPu]\{.*?(\}|$)" ); // (skip)
			pb.Add( @"(?'left_brace'\{).*?((?'right_brace'\})|$)" ); // '{...}'
			pb.Add( @"(?'left_bracket'\[) \]? (\\.|.)*? ((?'right_bracket'\])|$)" ); // '[...]'
			pb.Add( @"\\." ); // (skip)

			return pb.ToRegex( );
		}


		static string GetVersion( )
		{
			try
			{
				return WebView2Matcher.GetVersion( NonCancellable.Instance );
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
