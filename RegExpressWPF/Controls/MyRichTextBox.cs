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


		public MyRichTextBox( )
		{
		}


		public MyRichTextBox( FlowDocument document ) : base( document )
		{
		}


		internal TextData GetTextData( string eol, [CallerMemberName] string caller = null, [CallerLineNumber] int line = 0, [CallerFilePath] string file = null )
		{
			//...
			var t1 = Environment.TickCount;

			if( !mCachedTextData.TryGetTarget( out BaseTextData btd ) )
			{
				btd = RtbUtilities.GetBaseTextDataInternal( this, eol ?? "\n" );
				mCachedTextData.SetTarget( btd );
			}

			var td = RtbUtilities.GetTextDataInternal( this, btd, eol ?? btd.Eol );

			var t2 = Environment.TickCount;
			//...Debug.WriteLine( $"[][][] Getting text: {t2 - t1:F0} - {caller}:{line} '{Path.GetFileNameWithoutExtension( file )}'" );

			return td;
		}


		internal SimpleTextData GetSimpleTextData( string eol )
		{
			if( mCachedTextData.TryGetTarget( out BaseTextData btd ) )
			{
				return RtbUtilities.GetSimpleTextDataInternal( this, btd, eol ?? btd.Eol );
			}

			return RtbUtilities.GetSimpleTextDataInternal( this, eol );
		}


		protected override void OnTextChanged( TextChangedEventArgs e )
		{
			mCachedTextData.SetTarget( null );

			base.OnTextChanged( e );
		}
	}
}
