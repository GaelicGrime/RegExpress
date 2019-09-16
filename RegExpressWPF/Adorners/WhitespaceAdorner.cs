using RegExpressWPF.Code;
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


namespace RegExpressWPF.Adorners
{
    class WhitespaceAdorner : Adorner
    {
        readonly Pen WsPen = new Pen( Brushes.Transparent, 0 );
        readonly Brush WsBrush = Brushes.LightSeaGreen;
        readonly Pen TabPen = new Pen( Brushes.LightSeaGreen, 1 );
        readonly Pen EofPen = new Pen( Brushes.LightSeaGreen, 1 );
        readonly Brush EofBrush = Brushes.Transparent;


        public WhitespaceAdorner( UIElement adornedElement ) : base( adornedElement )
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

            ShowSpaces( dc, td );
            ShowTabs( dc, td );
            ShowEof( dc, td );
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


        void ShowSpaces( DrawingContext dc, TextData td )
        {
            var rtb = Rtb;

            var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );
            dc.PushClip( new RectangleGeometry( clip_rect ) );

            foreach( Match m in Regex.Matches( td.Text, @"(\p{Zs}+)(?:\n|\r|$)" ) )
            {
                var g = m.Groups[1];
                var start = td.Pointers[g.Index];
                var end = td.Pointers[g.Index + g.Length];
                if( start.HasValidLayout && end.HasValidLayout )
                {
                    var start_rect = start.GetCharacterRect( LogicalDirection.Forward );
                    var end_rect = end.GetCharacterRect( LogicalDirection.Backward );

                    // TODO: consider wrapped text

                    var w = end_rect.Right - start_rect.Left;

                    if( w > 0 )
                    {
                        var r = new Rect( start_rect.Left, start_rect.Top, w, start_rect.Height );

                        if( !r.IsEmpty && r.IntersectsWith( clip_rect ) )
                        {
                            var y = r.Top + r.Height / 2;

                            for( var i = 0; i < g.Length; ++i )
                            {
                                var x = r.Left + r.Width * i / g.Length + r.Width / g.Length / 2;

                                var p = new Rect( x, y, 2, 2 );

                                dc.DrawRectangle( WsBrush, WsPen, p );
                            }
                        }
                    }
                }
            }

            dc.Pop( );
        }


        void ShowTabs( DrawingContext dc, TextData td )
        {
            var rtb = Rtb;

            var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );
            dc.PushClip( new RectangleGeometry( clip_rect ) );

            var half_pen_th = TabPen.Thickness / 2;

            foreach( Match m in Regex.Matches( td.Text, @"\t" ) )
            {
                var start = td.Pointers[m.Index];

                if( start.HasValidLayout )
                {
                    var r = start.GetCharacterRect( LogicalDirection.Forward );
                    const int w = 8;
                    r.Width = w;
                    r.Offset( 2, 0 );
                    if( r.IntersectsWith( clip_rect ) )
                    {
                        var x = r.Left;
                        var y = r.Top + r.Height / 2.0;

                        var guidelines = new GuidelineSet( );
                        guidelines.GuidelinesY.Add( y - half_pen_th );

                        dc.PushGuidelineSet( guidelines );

                        dc.DrawLine( TabPen, new Point( x, y ), new Point( x + w, y ) );
                        dc.DrawLine( TabPen, new Point( x + w / 2, y - w / 2 ), new Point( x + w, y ) );
                        dc.DrawLine( TabPen, new Point( x + w / 2, y + w / 2 ), new Point( x + w, y ) );

                        dc.Pop( );
                    }
                }
            }

            dc.Pop( );
        }


        void ShowEof( DrawingContext dc, TextData td )
        {
            if( Regex.IsMatch( td.Text, @"(\r|\n)$", RegexOptions.ExplicitCapture ) )
            {
                var rtb = Rtb;

                if( rtb.Document.ContentEnd.HasValidLayout )
                {
                    var rect = rtb.Document.ContentEnd.GetCharacterRect( LogicalDirection.Forward ); // (no width)

                    if( !rect.IsEmpty )
                    {
                        double w = 5;
                        double h = rect.Height * 0.4;
                        double top = rect.Top + ( rect.Height - h ) / 2.0;

                        var eof_rect = new Rect( new Point( rect.Left + 3, top ), new Size( w, h ) );
                        var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );

                        dc.PushClip( new RectangleGeometry( clip_rect ) );

                        dc.DrawRectangle( EofBrush, EofPen, eof_rect );

                        dc.Pop( );
                    }
                }
            }
        }
    }
}

