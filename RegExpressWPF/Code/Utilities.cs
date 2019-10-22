using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

namespace RegExpressWPF.Code
{
	static class Utilities
	{
		public static int LineNumber( [CallerLineNumber] int lineNumber = 0 )
		{
			return lineNumber;
		}


		[Conditional( "DEBUG" )]
		public static void DbgSimpleLog( Exception exc, [CallerFilePath] string filePath = null, [CallerMemberName] string memberName = null, [CallerLineNumber] int lineNumber = 0 )
		{
			Debug.WriteLine( $"*** {exc.GetType( ).Name} in {memberName}:{lineNumber}" );
		}


		public static void DbgSaveXAML( string filename, FlowDocument doc )
		{
			var r = new TextRange( doc.ContentStart, doc.ContentEnd );

			using( var fs = File.OpenWrite( filename ) )
			{
				r.Save( fs, DataFormats.Xaml, true );
			}
		}
	}
}
