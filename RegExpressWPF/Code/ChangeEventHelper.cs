using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;


namespace RegExpressWPF.Code
{
    public sealed class ChangeEventHelper
    {
        int mChangeIndex = 0;
        readonly RichTextBox mRtb;
        //bool mIsFocused = false;


        public ChangeEventHelper( RichTextBox rtb )
        {
            mRtb = rtb;

            //mUIElement.GotFocus += UIElement_GotFocus;
            //mUIElement.LostFocus += UIElement_LostFocus;

            // TODO: consider "-="
        }


        public bool IsInChange => mChangeIndex != 0;


        public DispatcherOperation BeginInvoke( CancellationToken ct, Action action )
        {
            // TODO: Consider 'InvokeAsync'.
            return mRtb.Dispatcher.BeginInvoke( new Action( ( ) =>
            {
                if( !ct.IsCancellationRequested ) Do( action );
            } ), GetPriority( ) );
        }


        public void Invoke( CancellationToken ct, Action action )
        {
            mRtb.Dispatcher.Invoke( new Action( ( ) =>
            {
                Do( action );
            } ), GetPriority( ), ct );

            //DispatcherObject.Dispatcher.BeginInvoke( DispatcherPriority.Render, new Action( ( ) =>
            //{
            //    Do( a );
            //} ) ).Task.Wait( ct );

        }


        public void Do( Action action )
        {
			Debug.Assert( action != null );

            Interlocked.Increment( ref mChangeIndex );
            mRtb.BeginChange( );
            try
            {
                action( );
            }
			catch( Exception exc )
            {
				_ = exc;
                throw;
            }
			finally
			{
                mRtb.EndChange( );
                Interlocked.Decrement( ref mChangeIndex );
            }
        }


        //private void UIElement_GotFocus( object sender, RoutedEventArgs e )
        //{
        //    mIsFocused = true;
        //}


        //private void UIElement_LostFocus( object sender, RoutedEventArgs e )
        //{
        //    mIsFocused = false;
        //}


        static DispatcherPriority GetPriority( )
        {
            return DispatcherPriority.ApplicationIdle;
            //return DispatcherPriority.Background;
            //return mIsFocused ? DispatcherPriority.ContextIdle : DispatcherPriority.Background;
            //return DispatcherPriority.ContextIdle;
            //return mIsFocused ? DispatcherPriority.Background : DispatcherPriority.ContextIdle;
        }

    }
}
