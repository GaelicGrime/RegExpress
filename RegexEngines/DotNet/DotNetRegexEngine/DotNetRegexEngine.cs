using DotNetRegexEngineNs.Matches;
using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.SyntaxColouring;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;


namespace DotNetRegexEngineNs
{
	public class DotNetRegexEngine : IRegexEngine
	{
		readonly UCDotNetRegexOptions OptionsControl;

		static readonly Dictionary<RegexOptions, Regex> CachedColouringRegexes = new Dictionary<RegexOptions, Regex>( );
		static readonly Dictionary<RegexOptions, Regex> CachedHighlightingRegexes = new Dictionary<RegexOptions, Regex>( );


		static DotNetRegexEngine( )
		{
		}


		public DotNetRegexEngine( )
		{
			OptionsControl = new UCDotNetRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "DotNetRegex";

		public string Name => ".NET Regex";

		public string EngineVersion
		{
			get
			{
				try
				{
					// see: https://stackoverflow.com/questions/19096841

					System.Runtime.Versioning.TargetFrameworkAttribute target_framework_attribute =
						(System.Runtime.Versioning.TargetFrameworkAttribute)
						Assembly
							.GetExecutingAssembly( )
							.GetCustomAttributes( typeof( System.Runtime.Versioning.TargetFrameworkAttribute ), false )
							.SingleOrDefault( );

					if( target_framework_attribute == null ) return null;

					return Regex.Match( target_framework_attribute.FrameworkName, @"\d+(\.\d+)*", RegexOptions.ExplicitCapture | RegexOptions.Compiled ).Value;
				}
				catch( Exception exc )
				{
					_ = exc;
					if( Debugger.IsAttached ) Debugger.Break( );

					return null;
				}
			}
		}


		public RegexEngineCapabilityEnum Capabilities => RegexEngineCapabilityEnum.Default;

		public string NoteForCaptures => null;

		public event RegexEngineOptionsChanged OptionsChanged;


		public Control GetOptionsControl( )
		{
			return OptionsControl;
		}


		public RegexOptions RegexOptions => OptionsControl.CachedRegexOptions;


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
			RegexOptions selected_options = OptionsControl.CachedRegexOptions;
			TimeSpan timeout = OptionsControl.CachedTimeout;
			if( timeout <= TimeSpan.Zero ) timeout = TimeSpan.FromSeconds( 10 );

			var regex = new Regex( pattern, selected_options, timeout );

			return new DotNetMatcher( regex );
		}


		public void ColourisePattern( ICancellable cnc, ColouredSegments colouredSegments, string pattern, Segment visibleSegment )
		{
			Regex regex = GetCachedColouringRegex( OptionsControl.CachedRegexOptions );

			foreach( Match m in regex.Matches( pattern ) )
			{
				Debug.Assert( m.Success );

				if( cnc.IsCancellationRequested ) return;

				// comments, '(?#...)'
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

				// end-on-line comments, '#...', only if 'IgnorePatternWhitespace' option is specified
				{
					var g = m.Groups["eol_comment"];
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

				//if( cnc.IsCancellationRequested ) return;

				// character groups, '[...]'
				//{
				//	var g = m.Groups["char_group"];
				//	if( g.Success )
				//	{

				//	}
				//}

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
			int para_size = 1;
			Regex regex = GetCachedHighlightingRegex( OptionsControl.CachedRegexOptions );

			HighlightHelper.CommonHighlighting( cnc, highlights, pattern, selectionStart, selectionEnd, visibleSegment, regex, para_size );
		}

		#endregion IRegexEngine


		private void OptionsControl_Changed( object sender, EventArgs e )
		{
			OptionsChanged?.Invoke( this );
		}


		static Regex GetCachedColouringRegex( RegexOptions options )
		{
			options &= RegexOptions.IgnorePatternWhitespace; // filter unneeded flags

			lock( CachedColouringRegexes )
			{
				if( CachedColouringRegexes.TryGetValue( options, out Regex regex ) ) return regex;

				const string CommentPattern = @"(?'comment'\(\?\#.*?(\)|$))"; // including incomplete
				const string EolCommentPattern = @"(?'eol_comment'\#[^\n]*)";
				const string CharGroupPattern = @"(?'char_group'\[\]?(<<INTERIOR>>|.)*?(\]|$))"; // including incomplete
				const string EscapesPattern = @"(?'escape'
\\[0-7]{2,3} | 
\\x[0-9A-Fa-f]{1,2} | 
\\c[A-Za-z] | 
\\u[0-9A-Fa-f]{1,4} | 
\\(p|P)\{([A-Za-z]+\})? | 
\\k<([A-Za-z]+>)? |
\\.
)"; // including incomplete '\x', '\u', '\p', '\k'
				const string NamedGroupPattern = @"\(\?(?'name'((?'a'')|<)\p{L}\w*(-\p{L}\w*)?(?(a)'|>))"; // (balancing groups covered too)

				string pattern;

				if( options.HasFlag( RegexOptions.IgnorePatternWhitespace ) )
				{
					pattern =
						@"(?nsx)(" + Environment.NewLine +
							CommentPattern + " |" + Environment.NewLine +
							EolCommentPattern + " |" + Environment.NewLine +
							CharGroupPattern.Replace( "<<INTERIOR>>", EscapesPattern ) + " |" + Environment.NewLine +
							EscapesPattern + " |" + Environment.NewLine +
							NamedGroupPattern + " |" + Environment.NewLine +
							".(?!)" + Environment.NewLine +
						")";
				}
				else
				{
					pattern =
						@"(?nsx)(" + Environment.NewLine +
							CommentPattern + " |" + Environment.NewLine +
							//EolCommentPattern + " |" + Environment.NewLine +
							CharGroupPattern.Replace( "<<INTERIOR>>", EscapesPattern ) + " |" + Environment.NewLine +
							EscapesPattern + " |" + Environment.NewLine +
							NamedGroupPattern + " |" + Environment.NewLine +
							".(?!)" + Environment.NewLine +
						")";
				}

				regex = new Regex( pattern, RegexOptions.Compiled );

				CachedColouringRegexes.Add( options, regex );

				return regex;
			}
		}

		static Regex GetCachedHighlightingRegex( RegexOptions options )
		{
			options &= RegexOptions.IgnorePatternWhitespace; // keep this flag only

			lock( CachedHighlightingRegexes )
			{
				if( CachedHighlightingRegexes.TryGetValue( options, out Regex regex ) ) return regex;

				string pattern = "(?nsx)(";
				pattern += @"(\(\?\#.*?(\)|$)) | "; // comment
				if( options.HasFlag( RegexOptions.IgnorePatternWhitespace ) )
					pattern += @"(\#[^\n]*) | ";
				pattern += @"(?'left_para'\() | "; // '('
				pattern += @"(?'right_para'\)) | "; // ')'
				pattern += @"(?'char_group'\[(\\.|.)*?(\](?'end')|$)) | "; // '[...]'
				pattern += @"(?'range'\{\d+(,(\d+)?)?(\}(?'end')|$)) | "; // '{...}'
				pattern += @"(\\.)";
				pattern += @")";

				regex = new Regex( pattern, RegexOptions.Compiled );

				CachedHighlightingRegexes.Add( options, regex );

				return regex;
			}
		}
	}
}

