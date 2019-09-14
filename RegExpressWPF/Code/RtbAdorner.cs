using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace RegExpressWPF.Code
{
    class RtbAdorner : Adorner
    {
        readonly Pen EofPen = new Pen( Brushes.LightSeaGreen, 1 );
        readonly Brush EofBrush = Brushes.Transparent;


        public RtbAdorner( UIElement adornedElement ) : base( adornedElement )
        {
            Debug.Assert( adornedElement is RichTextBox );

            IsHitTestVisible = false;

            Rtb.TextChanged += Rtb_TextChanged;
            Rtb.AddHandler( ScrollViewer.ScrollChangedEvent, new RoutedEventHandler( Rtb_ScrollChanged ), true );
        }


        RichTextBox Rtb
        {
            get { return (RichTextBox)AdornedElement; }
        }


        private void Rtb_TextChanged( object sender, TextChangedEventArgs e )
        {
            Invalidate( );
        }


        private void Rtb_ScrollChanged( object sender, RoutedEventArgs e )
        {
            Invalidate( );
        }


        protected override void OnRender( DrawingContext drawingContext )
        {
            base.OnRender( drawingContext );  // (probably nothing)

            var dc = drawingContext;
            var rtb = Rtb;
            var td = RtbUtilities.GetTextData( rtb, "\n" );

            if( ShouldShowLastParagraphAsEmpty( td.Text ) )
            {
                var rect = rtb.Document.ContentEnd.GetCharacterRect( LogicalDirection.Forward ); // (no width)

                if( !rect.IsEmpty )
                {
                    double w = 5;
                    double h = rect.Height * 0.5;
                    double top = rect.Top + ( rect.Height - h ) / 2.0;

                    var eof_rect = new Rect( new Point( rect.Left + 3, top ), new Size( w, h ) );
                    var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );

                    dc.PushClip( new RectangleGeometry( clip_rect ) );

                    dc.DrawRectangle( EofBrush, EofPen, eof_rect );

                    dc.Pop( );
                }
            }
        }


        protected override void OnRenderSizeChanged( SizeChangedInfo sizeInfo )
        {
            base.OnRenderSizeChanged( sizeInfo );

            Invalidate( );
        }


        void Invalidate( )
        {
            Dispatcher.BeginInvoke( DispatcherPriority.Background, new Action( InvalidateVisual ) );
        }


        bool ShouldShowLastParagraphAsEmpty( string text )
        {
            return Regex.IsMatch( text, @"(\r|\n)$", RegexOptions.ExplicitCapture );
        }
    }
}

