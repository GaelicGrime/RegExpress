using RegExpressWPF.Code;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;


namespace RegExpressWPF.Adorners
{
    class UnderliningAdorner : Adorner
    {
        readonly Pen Pen = new Pen( Brushes.MediumVioletRed, 2 );

        List<Segment> Segments = null;
        string Eol;


        public UnderliningAdorner( UIElement adornedElement ) : base( adornedElement )
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

            var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );
            dc.PushClip( new RectangleGeometry( clip_rect ) );

            lock( this )
            {
                if( Segments != null && Segments.Any( ) )
                {
                    var td = RtbUtilities.GetTextData( rtb, Eol );

                    foreach( var segment in Segments )
                    {
                        if( segment.Index + segment.Length <= td.Text.Length ) //
                        {
                            var start = td.Pointers[segment.Index];
                            var end = td.Pointers[segment.Index + segment.Length];

                            if( start.HasValidLayout && end.HasValidLayout )
                            {
                                var start_rect = start.GetCharacterRect( LogicalDirection.Forward );
                                var end_rect = end.GetCharacterRect( LogicalDirection.Backward );

                                var u = Rect.Union( start_rect, end_rect );
                                if( u.IntersectsWith( clip_rect ) )
                                {
                                    if( start_rect.Bottom > end_rect.Top ) // no wrap, draw quickly
                                    {
                                        dc.DrawLine( Pen, start_rect.BottomLeft, end_rect.BottomRight );
                                        dc.DrawLine( Pen, start_rect.BottomLeft, new Point( start_rect.Left, start_rect.Bottom - 3 ) );
                                        dc.DrawLine( Pen, end_rect.BottomRight, new Point( end_rect.Right, end_rect.Bottom - 3 ) );
                                    }
                                    else
                                    {
                                        // wrap; needs more work

                                        TextPointer left = start;

                                        while( left.CompareTo( end ) < 0 )
                                        {
                                            Rect left_rect = left.GetCharacterRect( LogicalDirection.Forward );
                                            var right = left.GetNextInsertionPosition( LogicalDirection.Forward );

                                            for( ; right != null; )
                                            {
                                                if( right.CompareTo( end ) >= 0 ) break;

                                                var right_rect_forward = right.GetCharacterRect( LogicalDirection.Forward );
                                                if( right_rect_forward.Top > left_rect.Bottom ) break;

                                                right = right.GetNextInsertionPosition( LogicalDirection.Forward );
                                            }

                                            if( right == null || right.CompareTo( end ) > 0 ) right = end;

                                            var right_rect_backward = right.GetCharacterRect( LogicalDirection.Backward );

                                            dc.DrawLine( Pen, left_rect.BottomLeft, right_rect_backward.BottomRight );
                                            if( left == start )
                                            {
                                                dc.DrawLine( Pen, left_rect.BottomLeft, new Point( left_rect.Left, left_rect.Bottom - 3 ) );
                                            }
                                            if( right.CompareTo( end ) == 0 )
                                            {
                                                dc.DrawLine( Pen, right_rect_backward.BottomRight, new Point( right_rect_backward.Right, right_rect_backward.Bottom - 3 ) );
                                            }

                                            left = right;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            dc.Pop( );
        }


        void Invalidate( )
        {
            Dispatcher.BeginInvoke( DispatcherPriority.Background, new Action( InvalidateVisual ) );
        }


        internal void SetSegmentsToUnderline( List<Segment> segments_to_underline, string eol )
        {
            lock( this )
            {
                Segments = segments_to_underline;
                Eol = eol;
                Dispatcher.Invoke( new Action( ( ) => Invalidate( ) ) );
            }
        }
    }
}
