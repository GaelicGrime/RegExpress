using RegExpressWPF.Code;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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


		internal TextData GetTextData( string eol )
		{
			if( !mCachedTextData.TryGetTarget( out BaseTextData btd ) )
			{
				btd = RtbUtilities.GetBaseTextData( this, eol ?? "\n" );
				mCachedTextData.SetTarget( btd );
			}

			return RtbUtilities.GetTextData( this, btd, eol ?? btd.Eol );
		}


		protected override void OnTextChanged( TextChangedEventArgs e )
		{
			mCachedTextData.SetTarget( null );

			base.OnTextChanged( e );
		}
	}
}
