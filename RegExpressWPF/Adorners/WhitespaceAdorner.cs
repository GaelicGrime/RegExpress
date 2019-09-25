using RegExpressWPF.Code;
using RegExpressWPF.Controls;
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
        readonly Brush WsBrush = Brushes.LightSeaGreen;
        readonly Pen TabPen = new Pen( Brushes.LightSeaGreen, 1 );
        readonly Pen EolPen = new Pen( Brushes.LightSeaGreen, 1 );
        readonly Pen EofPen = new Pen( Brushes.LightSeaGreen, 1 );
        readonly Brush EofBrush = Brushes.Transparent;

        readonly Regex RegexSpaces = new Regex( @"(\p{Zs})\1*", RegexOptions.Compiled );
        readonly Regex RegexTabs = new Regex( @"\t", RegexOptions.Compiled );
        readonly Regex RegexEols = new Regex( @"\r\n|\n\r|\r|\n", RegexOptions.Compiled );

        bool mShowWhitespaces = false;


        public WhitespaceAdorner( UIElement adornedElement ) : base( adornedElement )
        {
            Debug.Assert( adornedElement is MyRichTextBox );

            IsHitTestVisible = false;

            Rtb.TextChanged += Rtb_TextChanged;
            Rtb.AddHandler( ScrollViewer.ScrollChangedEvent, new RoutedEventHandler( Rtb_ScrollChanged ), true );
        }


        public void ShowWhitespaces( bool yes )
        {
            mShowWhitespaces = yes;

            Invalidate( );
        }


        MyRichTextBox Rtb
        {
            get { return (MyRichTextBox)AdornedElement; }
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

            if( mShowWhitespaces )
            {
                var dc = drawingContext;
                var rtb = Rtb;
                var td = rtb.GetTextData( null );

                ShowSpaces( dc, td );
                ShowTabs( dc, td );
                ShowEols( dc, td );
                ShowEof( dc, td );
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


        void ShowSpaces( DrawingContext dc, TextData td )
        {
            var rtb = Rtb;
            var start_doc = Rtb.Document.ContentStart;

            var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );
            dc.PushClip( new RectangleGeometry( clip_rect ) );

            foreach( Match match in RegexSpaces.Matches( td.Text ) )
            {
                var left = td.Pointers[match.Index];
                var right = td.Pointers[match.Index + match.Length];

                if( left.HasValidLayout && right.HasValidLayout &&
                    left.IsInSameDocument( start_doc ) && right.IsInSameDocument( start_doc ) )
                {
                    left = left.GetInsertionPosition( LogicalDirection.Forward );
                    right = right.GetInsertionPosition( LogicalDirection.Backward );

                    var rect_left = left.GetCharacterRect( LogicalDirection.Forward );
                    var rect_right = right.GetCharacterRect( LogicalDirection.Backward );

                    var rect = new Rect( rect_left.TopLeft, rect_right.BottomRight );

                    if( !rect.IsEmpty && rect.IntersectsWith( clip_rect ) )
                    {
                        const int DOT_SIZE = 2;
                        var y = Math.Floor( rect.Top + rect.Height / 2 - DOT_SIZE / 2 + 0.5 );
                        var w = rect.Width / match.Length;

                        for( var i = 0; i < match.Length; ++i )
                        {
                            var x = rect.Left + rect.Width * i / match.Length + w / 2;
                            var p = new Rect( x, y, DOT_SIZE, DOT_SIZE );

                            dc.DrawRectangle( WsBrush, null, p );
                        }
                    }
                }
            }

            dc.Pop( );
        }


        void ShowTabs( DrawingContext dc, TextData td )
        {
            var rtb = Rtb;
            var start_doc = Rtb.Document.ContentStart;

            var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );
            dc.PushClip( new RectangleGeometry( clip_rect ) );

            foreach( Match match in RegexTabs.Matches( td.Text ) )
            {
                var left = td.Pointers[match.Index];

                if( left.HasValidLayout && left.IsInSameDocument( start_doc ) )
                {
                    left = left.GetInsertionPosition( LogicalDirection.Forward );

                    var rect = left.GetCharacterRect( LogicalDirection.Forward );
                    const int ARROW_WIDTH = 6;
                    rect.Width = ARROW_WIDTH;
                    rect.Offset( 2, 0 );

                    if( rect.IntersectsWith( clip_rect ) )
                    {
                        var x = Math.Ceiling( rect.Left ) + TabPen.Thickness / 2;
                        var y = Math.Ceiling( rect.Top + rect.Height / 2 ) - TabPen.Thickness / 2;

                        dc.DrawLine( TabPen, new Point( x, y ), new Point( x + ARROW_WIDTH, y ) );
                        dc.DrawLine( TabPen, new Point( x + ARROW_WIDTH / 2, y - ARROW_WIDTH / 2 ), new Point( x + ARROW_WIDTH, y ) );
                        dc.DrawLine( TabPen, new Point( x + ARROW_WIDTH / 2, y + ARROW_WIDTH / 2 ), new Point( x + ARROW_WIDTH, y ) );
                    }
                }
            }

            dc.Pop( );
        }


        void ShowEols( DrawingContext dc, TextData td )
        {
            var rtb = Rtb;
            var start_doc = Rtb.Document.ContentStart;

            var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );
            dc.PushClip( new RectangleGeometry( clip_rect ) );

            foreach( Match match in RegexEols.Matches( td.Text ) )
            {
                var left = td.Pointers[match.Index];

                if( left.HasValidLayout && left.IsInSameDocument( start_doc ) )
                {
                    left = left.GetInsertionPosition( LogicalDirection.Forward );

                    var rect = left.GetCharacterRect( LogicalDirection.Forward );
                    const int EOL_WIDTH = 6;
                    rect.Width = EOL_WIDTH;
                    rect.Offset( 2, 0 );

                    if( rect.IntersectsWith( clip_rect ) )
                    {
                        var x = Math.Ceiling( rect.Left ) + EolPen.Thickness / 2;
                        var y = Math.Ceiling( rect.Top + rect.Height / 2 ) - EolPen.Thickness / 2;

                        dc.DrawLine( EolPen, new Point( x, y ), new Point( x + EOL_WIDTH, y ) );
                        dc.DrawLine( EolPen, new Point( x + EOL_WIDTH, y ), new Point( x + EOL_WIDTH, y - rect.Height * 0.45 ) );
                        dc.DrawLine( EolPen, new Point( x, y ), new Point( x + EOL_WIDTH / 2, y - EOL_WIDTH / 2 ) );
                        dc.DrawLine( EolPen, new Point( x, y ), new Point( x + EOL_WIDTH / 2, y + EOL_WIDTH / 2 ) );
                    }
                }
            }

            dc.Pop( );
        }

        void ShowEof( DrawingContext dc, TextData td )
        {
            //if( Regex.IsMatch( td.Text, @"(\r|\n)$", RegexOptions.ExplicitCapture ) )
            {
                var rtb = Rtb;

                if( rtb.Document.ContentEnd.HasValidLayout )
                {
                    var rect = rtb.Document.ContentEnd.GetCharacterRect( LogicalDirection.Forward ); // (no width)

                    if( !rect.IsEmpty )
                    {
                        var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );

                        dc.PushClip( new RectangleGeometry( clip_rect ) );

                        const double EOF_WIDTH = 5;
                        double h = Math.Ceiling( rect.Height * 0.4 );

                        var x = Math.Ceiling( rect.Left + 3 ) + EofPen.Thickness / 2;
                        var y = Math.Floor( rect.Top + ( rect.Height - h ) / 2 ) + EofPen.Thickness / 2;

                        var eof_rect = new Rect( x, y, EOF_WIDTH, h );

                        dc.DrawRectangle( EofBrush, EofPen, eof_rect );

                        dc.Pop( );
                    }
                }
            }
        }
    }
}

