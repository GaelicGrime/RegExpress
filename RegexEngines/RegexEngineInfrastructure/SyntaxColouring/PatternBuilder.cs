using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RegexEngineInfrastructure.SyntaxColouring
{
	public sealed class PatternBuilder
	{
		readonly StringBuilder Sb;
#if DEBUG
		const string Prefix = "(?nsx)(\r\n";
		const string Or = " |\r\n";
		const string Suffix = ")";
#else
		const string Prefix = "(?nsx)(";
		const string Or = "|";
		const string Suffix = ")";
#endif
		const string AlwaysFalsePattern = "(?!)";

		int LengthBeforeGroup;
		int LengthAfterGroupHeader;


		public PatternBuilder( )
		{
			Sb = new StringBuilder( Prefix );
			LengthBeforeGroup = -1;
			LengthAfterGroupHeader = -1;
		}

		public PatternBuilder Add( string pattern )
		{
			if( string.IsNullOrEmpty( pattern ) ) return this;

			Debug.Assert( !Regex.IsMatch( pattern, @"^\s*\| | \|\s*$", RegexOptions.IgnorePatternWhitespace ) );

			if( LengthBeforeGroup > 0 ) // group in progress?
			{
				Debug.Assert( LengthAfterGroupHeader > LengthBeforeGroup );

				if( Sb.Length > LengthAfterGroupHeader ) Sb.Append( Or );
			}
			else
			{
				if( Sb.Length > Prefix.Length ) Sb.Append( Or );
			}

			Sb.Append( pattern );

			return this;
		}


		public PatternBuilder Add( params string[] patterns )
		{
			foreach( var pattern in patterns )
			{
				Add( pattern );
			}

			return this;
		}


		public PatternBuilder BeginGroup( string name )
		{
			if( LengthBeforeGroup >= 0 ) throw new InvalidOperationException( "Group already in progress" );

			LengthBeforeGroup = Sb.Length;

			if( Sb.Length > Prefix.Length ) Sb.Append( Or );

			if( string.IsNullOrEmpty( name ) )
				Sb.Append( "(" );
			else
				Sb.Append( @"(?'" ).Append( name ).Append( @"'" );

			LengthAfterGroupHeader = Sb.Length;

			return this;
		}


		public PatternBuilder EndGroup( )
		{
			if( LengthBeforeGroup < 0 ) throw new InvalidOperationException( "No group in progress" );

			Debug.Assert( LengthBeforeGroup < Sb.Length );
			Debug.Assert( LengthAfterGroupHeader > LengthBeforeGroup );

			if( Sb.Length == LengthAfterGroupHeader ) // nothing added to group?
			{
				Sb.Length = LengthBeforeGroup;
			}
			else
			{
				Sb.Append( ')' );
			}

			LengthBeforeGroup = -1;
			LengthAfterGroupHeader = -1;

			return this;
		}


		public PatternBuilder AddGroup( string name, string pattern )
		{
			return BeginGroup( name ).Add( pattern ).EndGroup( );
		}


		public string ToPattern( )
		{
			if( LengthBeforeGroup >= 0 ) throw new InvalidOperationException( "Group in progress" );

			return Sb.Length == Prefix.Length ? AlwaysFalsePattern : ( Sb.ToString( ) + Suffix );
		}


		public Regex ToRegex( )
		{
			return new Regex( ToPattern( ), RegexOptions.Compiled | RegexOptions.ExplicitCapture );
		}

	}
}
