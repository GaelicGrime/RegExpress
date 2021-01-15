using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
using RegexEngineInfrastructure.Matches.Simple;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;


namespace IcuRegexEngineNs
{
	class IcuMatcher : IMatcher, ISimpleTextGetter
	{
		readonly IcuRegexOptions Options;
		readonly string Pattern;
		string Text;


		public IcuMatcher( string pattern, IcuRegexOptions options )
		{
			Options = options;

			if( !string.IsNullOrWhiteSpace( options.Limit ) && !int.TryParse( options.Limit, out var limit ) )
			{
				throw new ApplicationException( "Invalid limit. Please enter an integer number." );
			}

			Pattern = pattern;
		}


		public static string GetIcuVersion( ICancellable cnc )
		{
			MemoryStream stdout_contents;
			string stderr_contents;

			Action<Stream> stdinWriter = s =>
			{
				using( var bw = new BinaryWriter( s, Encoding.Unicode, leaveOpen: false ) )
				{
					bw.Write( "v" );
				}
			};

			if( !ProcessUtilities.InvokeExe( cnc, GetIcuClientExePath( ), null, stdinWriter, out stdout_contents, out stderr_contents, EncodingEnum.Unicode ) )
			{
				return "Unknown version";
			}

			using( var br = new BinaryReader( stdout_contents, Encoding.Unicode ) )
			{
				string version = br.ReadString( );

				return version;
			}
		}


		#region IMatcher

		public RegexMatches Matches( string text, ICancellable cnc )
		{
			Text = text;

			MemoryStream stdout_contents;
			string stderr_contents;

			int limit;
			if( !int.TryParse( Options.Limit, out limit ) ) limit = 0;

			uint flags = 0;
			if( Options.UREGEX_CANON_EQ ) flags |= 1 << 0;
			if( Options.UREGEX_CASE_INSENSITIVE ) flags |= 1 << 1;
			if( Options.UREGEX_COMMENTS ) flags |= 1 << 2;
			if( Options.UREGEX_DOTALL ) flags |= 1 << 3;
			if( Options.UREGEX_LITERAL ) flags |= 1 << 4;
			if( Options.UREGEX_MULTILINE ) flags |= 1 << 5;
			if( Options.UREGEX_UNIX_LINES ) flags |= 1 << 6;
			if( Options.UREGEX_UWORD ) flags |= 1 << 7;
			if( Options.UREGEX_ERROR_ON_UNKNOWN_ESCAPES ) flags |= 1 << 8;

#if DEBUG
			{
				// For debugging
				using( var fs = File.Create( "debug-icu.dat" ) )
				{
					using( var bw = new BinaryWriter( fs, Encoding.Unicode ) )
					{
						bw.Write( "m" );
						bw.Write( Pattern );
						bw.Write( Text );
						bw.Write( flags );
						bw.Write( limit );
					}
				}
			}
#endif

			Action<Stream> stdinWriter = s =>
			{
				using( var bw = new BinaryWriter( s, Encoding.Unicode, leaveOpen: false ) )
				{
					bw.Write( "m" );
					bw.Write( Pattern );
					bw.Write( Text );
					bw.Write( flags );
					bw.Write( limit );
				}
			};

			if( !ProcessUtilities.InvokeExe( cnc, GetIcuClientExePath( ), null, stdinWriter, out stdout_contents, out stderr_contents, EncodingEnum.Unicode ) )
			{
				return RegexMatches.Empty;
			}

			if( !string.IsNullOrWhiteSpace( stderr_contents ) )
			{
				throw new Exception( stderr_contents );
			}

			using( var br = new BinaryReader( stdout_contents, Encoding.Unicode ) )
			{
				// read group names

				var group_names = new Dictionary<int, string>( );

				for(; ; )
				{
					int i = br.ReadInt32( );
					if( i <= 0 ) break;

					string name = br.ReadString( );

					group_names.Add( i, name );
				}

				// read matches

				List<IMatch> matches = new List<IMatch>( );

				for(; ; )
				{
					int group_count = br.ReadInt32( );
					if( group_count < 0 ) break;

					SimpleMatch match = null; ;

					for( int i = 0; i <= group_count; ++i )
					{
						int start = br.ReadInt32( );
						bool success = start >= 0;
						int end;
						int length;
						if( success )
						{
							end = br.ReadInt32( );
							length = success ? end - start : 0;
						}
						else
						{
							end = 0;
							length = 0;
						}

						if( i == 0 )
						{
							Debug.Assert( success );
							Debug.Assert( match == null );

							match = SimpleMatch.Create( start, length, this );
							match.AddGroup( start, length, success, "0" );
						}
						else
						{
							string name;

							if( !group_names.TryGetValue( i, out name ) )
							{
								name = i.ToString( CultureInfo.InvariantCulture );
							}

							match.AddGroup( start, length, success, name );
						}
					}

					Debug.Assert( match != null );

					matches.Add( match );
				}

				return new RegexMatches( matches.Count, matches );
			}
		}

		#endregion IMatcher


		#region ISimpleTextGetter

		public string GetText( int index, int length )
		{
			return Text.Substring( index, length );
		}

		#endregion ISimpleTextGetter


		static string GetIcuClientExePath( )
		{
			string assembly_location = Assembly.GetExecutingAssembly( ).Location;
			string assembly_dir = Path.GetDirectoryName( assembly_location );
			string d_client_exe = Path.Combine( assembly_dir, @"IcuClient.bin" );

			return d_client_exe;
		}
	}
}
