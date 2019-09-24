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

        IReadOnlyList<(TextPointer start, TextPointer end)> Ranges = null;


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
                if( Ranges != null )
                {
                    var start_doc = rtb.Document.ContentStart;

                    // TODO: clean 'Ranges' if document was changed (release old document)

                    foreach( var (start, end) in Ranges )
                    {
                        if( start.HasValidLayout && end.HasValidLayout )
                        {
                            if( start.IsInSameDocument( start_doc ) && end.IsInSameDocument( start_doc ) )
                            {
                                var start_rect = start.GetCharacterRect( LogicalDirection.Forward );
                                var end_rect = end.GetCharacterRect( LogicalDirection.Backward );

                                if( !( end_rect.Bottom < clip_rect.Top || start_rect.Top > clip_rect.Bottom ) )
                                {
                                    //if( start_rect.Bottom > end_rect.Top )
                                    //{
                                    //    // no wrap, draw quickly

                                    //    var guidelines = new GuidelineSet( );
                                    //    guidelines.GuidelinesY.Add( start_rect.Bottom );

                                    //    dc.PushGuidelineSet( guidelines );

                                    //    dc.DrawLine( Pen, start_rect.BottomLeft, end_rect.BottomLeft );
                                    //    dc.DrawLine( Pen, start_rect.BottomLeft, new Point( start_rect.Left, start_rect.Bottom - 3 ) );
                                    //    dc.DrawLine( Pen, end_rect.BottomLeft, new Point( end_rect.Left, end_rect.Bottom - 3 ) );

                                    //    dc.Pop( );
                                    //}
                                    //else
                                    {
                                        /*
                                            // wrap; needs more work

                                            var guidelines = new GuidelineSet( );

                                            TextPointer left = start;

                                            do
                                            {
                                                TextPointer prev_right;
                                                Rect right_rect;

                                                Rect left_rect = left.GetCharacterRect( LogicalDirection.Forward );
                                                TextPointer right = left;

                                                for(; ; )
                                                {
                                                    prev_right = right;
                                                    right = right.GetNextInsertionPosition( LogicalDirection.Forward );
                                                    if( right == null || right.CompareTo( end ) >= 0 )
                                                    {
                                                        right = end;
                                                        break;
                                                    }

                                                    right_rect = right.GetCharacterRect( LogicalDirection.Forward );
                                                    if( right_rect.Top > left_rect.Bottom ) break;
                                                }

                                                if( right == end )
                                                {
                                                    right_rect = end.GetCharacterRect( LogicalDirection.Forward );
                                                }
                                                else
                                                {
                                                    right_rect = prev_right.GetCharacterRect( LogicalDirection.Forward ); // (does not include width)
                                                                                                                          // TODO: offset in case of wrapped text; now the last character is not underlined
                                                }

                                                guidelines.GuidelinesY.Clear( );
                                                guidelines.GuidelinesY.Add( left_rect.Bottom );

                                                dc.PushGuidelineSet( guidelines );

                                                dc.DrawLine( Pen, left_rect.BottomLeft, right_rect.BottomRight );
                                                if( left == start )
                                                {
                                                    dc.DrawLine( Pen, left_rect.BottomLeft, new Point( left_rect.Left, left_rect.Bottom - 3 ) );
                                                }
                                                if( right == end )
                                                {
                                                    dc.DrawLine( Pen, right_rect.BottomRight, new Point( right_rect.Right, right_rect.Bottom - 3 ) );
                                                }

                                                dc.Pop( );

                                                left = right;

                                                Debug.Assert( left != null );

                                            } while( left != end );
                                        */

                                        

                                        var adjusted_end = end.GetInsertionPosition( LogicalDirection.Backward );
                                        Debug.Assert( adjusted_end != null );
                                        Debug.Assert( adjusted_end.IsAtInsertionPosition );

                                        TextPointer tp = start;
                                        if( !tp.IsAtInsertionPosition ) tp = tp.GetInsertionPosition( LogicalDirection.Forward );
                                        Debug.Assert( tp.IsAtInsertionPosition );

                                        RectInfo rect_info = new RectInfo { nextRect = start_rect };

                                        for(; ; )
                                        {
                                            Rect r = Rect.Empty;

                                            for(; ; )
                                            {
                                                rect_info = GetRect( tp, rect_info.nextRect );
                                                r.Union( rect_info.thisRect );

                                                if( rect_info.nextPointer == null || !IsBefore( rect_info.nextPointer, adjusted_end ) ) { tp = null; break; }
                                                tp = rect_info.nextPointer;
                                                if( !rect_info.nextIsSameLine ) { break; }
                                            }

                                            dc.DrawLine( Pen, r.BottomLeft, r.BottomRight );

                                            if( tp == null ) break;
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


        struct RectInfo
        {
            public Rect thisRect;
            public TextPointer nextPointer;
            public Rect nextRect;
            public bool nextIsSameLine;
        }


        RectInfo GetRect( TextPointer thisPointer, Rect thisLeadingRect )
        {
            Debug.Assert( thisPointer.IsAtInsertionPosition );
            var nextPointer = thisPointer.GetNextInsertionPosition( LogicalDirection.Forward );
            Debug.Assert( nextPointer.IsAtInsertionPosition ); //.....

            if( nextPointer == null )
            {
                return new RectInfo
                {
                    thisRect = new Rect( thisLeadingRect.TopLeft, new Size( 0, thisLeadingRect.Height ) ), //.......... 10?
                    nextPointer = null,
                    nextRect = Rect.Empty,
                    nextIsSameLine = false
                };
            }

            var next_rect = nextPointer.GetCharacterRect( LogicalDirection.Forward );

            if( next_rect.Top < thisLeadingRect.Bottom )
            {
                return new RectInfo
                {
                    thisRect = new Rect( thisLeadingRect.TopLeft, next_rect.BottomLeft ),
                    nextPointer = nextPointer,
                    nextRect = next_rect,
                    nextIsSameLine = true
                };
            }
            else
            {
                return new RectInfo
                {
                    thisRect = new Rect( thisLeadingRect.TopLeft, new Size( 10, thisLeadingRect.Height ) ), //.......... 10?
                    nextPointer = nextPointer,
                    nextRect = next_rect,
                    nextIsSameLine = false
                };
            }
        }


        //(TextPointer nextLeft, Rect rect) Advance(TextPointer left, TextPointer end)
        //{
        //    Debug.Assert( left.IsAtInsertionPosition );

        //    if( !IsBefore( left, end ) ) return (null, Rect.Empty);

        //    Rect r_left = left.GetCharacterRect( LogicalDirection.Forward );

        //    TextPointer tp = left;

        //    for( ; ;)
        //    {
        //        var tp_next = tp.GetNextInsertionPosition( LogicalDirection.Forward );
        //        Debug.Assert( tp_next != null );

        //        Rect r_next = tp_next.GetCharacterRect( LogicalDirection.Forward );

        //        if( tp.GetLineStartPosition)

        //    }

        //    return null;
        //}


        static bool IsBefore( TextPointer tp1, TextPointer tp2 )
        {
            return tp1.CompareTo( tp2 ) < 0;
        }


        internal void SetRangesToUnderline( IReadOnlyList<(TextPointer start, TextPointer end)> ranges )
        {
            lock( this )
            {
                Ranges = ranges;
            }

            Invalidate( );
        }
    }
}
