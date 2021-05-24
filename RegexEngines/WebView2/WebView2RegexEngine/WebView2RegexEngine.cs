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


		public WebView2RegexEngine( )
		{
			OptionsControl = new UCWebView2RegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "WebView2Regex";

		public string Name => "WebView2";

		public string EngineVersion => LazyVersion.Value;

		public RegexEngineCapabilityEnum Capabilities => RegexEngineCapabilityEnum.NoCaptures;

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

			//.........
			return PatternBuilder.AlwaysFailsRegex;

			/*if( options.UREGEX_LITERAL ) return PatternBuilder.AlwaysFailsRegex;

			object key = options.UREGEX_COMMENTS;

			lock( CachedColouringRegexes )
			{
				if( CachedColouringRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateColouringRegex( options );

				CachedColouringRegexes.Add( key, regex );

				return regex;
			}*/
		}


		Regex GetCachedHighlightingRegex( )
		{
			WebView2RegexOptions options = OptionsControl.GetSelectedOptions( );

			//.............
			return PatternBuilder.AlwaysFailsRegex;

			/*if( options.UREGEX_LITERAL ) return PatternBuilder.AlwaysFailsRegex;

			object key = options.UREGEX_COMMENTS;

			lock( CachedHighlightingRegexes )
			{
				if( CachedHighlightingRegexes.TryGetValue( key, out Regex regex ) ) return regex;

				regex = CreateHighlightingRegex( options );

				CachedHighlightingRegexes.Add( key, regex );

				return regex;
			}*/
		}


		Regex CreateColouringRegex( WebView2RegexOptions options )
		{
			//.............
			return PatternBuilder.AlwaysFailsRegex;
			/*
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
			*/
		}


		Regex CreateHighlightingRegex( WebView2RegexOptions options )
		{
			//.............
			return PatternBuilder.AlwaysFailsRegex;

			/*var pb = new PatternBuilder( );

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
			*/
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
