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
        readonly Dictionary<string /* eol */, WeakReference<TextData>> mTextDataByEol = new Dictionary<string, WeakReference<TextData>>( );


        public MyRichTextBox( )
        {
            mTextDataByEol.Add( "\n", new WeakReference<TextData>( null ) );
            mTextDataByEol.Add( "\r", new WeakReference<TextData>( null ) );
            mTextDataByEol.Add( "\r\n", new WeakReference<TextData>( null ) );
        }


        public MyRichTextBox( FlowDocument document ) : this( )
        {
        }


        internal TextData GetTextData( string eol )
        {
            TextData td;

            if( eol == null )
            {
                // try any

                foreach( var v in mTextDataByEol.Values )
                {
                    if( v.TryGetTarget( out td ) )
                    {
                        Debug.Assert( td != null );

                        RtbUtilities.UpdateSelection( this, td );

                        return td;
                    }
                }

                eol = "\n";
            }

            Debug.Assert( eol == "\r\n" || eol == "\n\r" || eol == "\r" || eol == "\n" );

            if( !mTextDataByEol.TryGetValue( eol, out WeakReference<TextData> wr ) )
            {
                td = RtbUtilities.GetTextData( this, eol );

                mTextDataByEol.Add( eol, new WeakReference<TextData>( td ) );
            }
            else
            {
                if( !wr.TryGetTarget( out td ) )
                {
                    td = RtbUtilities.GetTextData( this, eol );

                    wr.SetTarget( td );
                }
            }

            Debug.Assert( td != null );
            Debug.Assert( td.Eol == eol );
            Debug.Assert( mTextDataByEol.ContainsKey( eol ) );
            Debug.Assert( mTextDataByEol[eol].TryGetTarget( out TextData dbg_td ) && object.ReferenceEquals( dbg_td, td ) );

            RtbUtilities.UpdateSelection( this, td );

            return td;
        }


        protected override void OnTextChanged( TextChangedEventArgs e )
        {
            foreach( var wr in mTextDataByEol.Values ) wr.SetTarget( null );

            base.OnTextChanged( e );
        }
    }
}
