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
        readonly Pen Pen = new Pen( Brushes.MediumVioletRed, 1 );

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

            //.............

            //Pen TabPen = new Pen( Brushes.Red, 2 );

            //if( td.Text.Length > 20 )
            //{
            //    var start = td.Pointers[7];
            //    var end = td.Pointers[17];

            //    if( start.HasValidLayout )
            //    {
            //        var start_rect = start.GetCharacterRect( LogicalDirection.Forward );
            //        var end_rect = end.GetCharacterRect( LogicalDirection.Backward );

            //        dc.DrawLine( TabPen, start_rect.BottomLeft, end_rect.BottomRight );
            //    }
            //}

            lock( this )
            {
                if( Segments != null && Segments.Any() )
                {
                    var td = RtbUtilities.GetTextData( rtb, Eol );

                    foreach( var segment in Segments )
                    {
                        if( segment.Index + segment.Length < td.Text.Length ) //...
                        {
                            var start = td.Pointers[segment.Index];
                            var end = td.Pointers[segment.Index + segment.Length];

                            if( start.HasValidLayout )
                            {
                                var start_rect = start.GetCharacterRect( LogicalDirection.Forward );
                                var end_rect = end.GetCharacterRect( LogicalDirection.Backward );

                                var u = Rect.Union( start_rect, end_rect );
                                if( u.IntersectsWith( clip_rect ) )
                                {
                                    dc.DrawLine( Pen, start_rect.BottomLeft, end_rect.BottomRight );
                                    dc.DrawLine( Pen, start_rect.BottomLeft, new Point( start_rect.Left, start_rect.Bottom - 3 ) );
                                    dc.DrawLine( Pen, end_rect.BottomRight, new Point( end_rect.Right, end_rect.Bottom - 3 ) );
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
