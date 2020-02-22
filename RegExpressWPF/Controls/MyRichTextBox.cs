using RegExpressWPF.Code;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;


namespace RegExpressWPF.Controls
{
	internal class MyRichTextBox : RichTextBox
	{
		readonly WeakReference<BaseTextData> mCachedTextData = new WeakReference<BaseTextData>( null );
		readonly WeakReference<SimpleTextData> mCachedSimpleTextData = new WeakReference<SimpleTextData>( null );

		public int LastGetTextDataDuration { get; private set; } = 0;


		public MyRichTextBox( )
		{
		}


		public MyRichTextBox( FlowDocument document ) : base( document )
		{
		}


		internal TextData GetTextData( string eol, [CallerMemberName] string caller = null, [CallerLineNumber] int line = 0, [CallerFilePath] string file = null )
		{
			var t1 = Environment.TickCount;

			TextData td;

			if( mCachedTextData.TryGetTarget( out BaseTextData btd ) )
			{
				// nothing
			}
			else
			{
				btd = RtbUtilities.GetBaseTextDataInternal( this, eol ?? "\n" );
				mCachedTextData.SetTarget( btd );
			}

			td = RtbUtilities.GetTextDataFrom( this, btd, eol ?? btd.Eol );

			var t2 = Environment.TickCount;

			LastGetTextDataDuration = t2 - t1; // (wrapping is ignored)

			//Debug.WriteLine( $"####### GetTextData: {LastGetTextDataDuration:F0} - {caller}:{line} '{Path.GetFileNameWithoutExtension( file )}'" );

			return td;
		}


		internal SimpleTextData GetSimpleTextData( string eol, [CallerMemberName] string caller = null, [CallerFilePath] string callerPath = null, [CallerLineNumber] int callerLine = 0 )
		{
			//...
			var t1 = Environment.TickCount;

			SimpleTextData std;

			if( mCachedSimpleTextData.TryGetTarget( out std ) )
			{
				std = RtbUtilities.GetSimpleTextDataFrom( std, eol ?? std.Eol );
			}
			else if( mCachedTextData.TryGetTarget( out BaseTextData btd ) )
			{
				std = RtbUtilities.GetSimpleTextDataFrom( this, btd, eol ?? btd.Eol );
			}
			else
			{
				std = RtbUtilities.GetSimpleTextDataInternal( this, eol ?? "\n" );
				mCachedSimpleTextData.SetTarget( std );
			}

			var t2 = Environment.TickCount;
			//Debug.WriteLine( $"####### GetSimpleTextData {t2 - t1:F0}: {caller} - {Path.GetFileNameWithoutExtension( callerPath )}:{callerLine}" );

			return std;
		}


		protected override void OnTextChanged( TextChangedEventArgs e )
		{
			mCachedTextData.SetTarget( null );
			mCachedSimpleTextData.SetTarget( null );

			base.OnTextChanged( e );
		}
	}
}
