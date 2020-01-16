using RegexEngineInfrastructure;
using RegExpressWPF.Code;
using RegExpressWPF.Controls;
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
	class PatternHighlightsAdorner : Adorner
	{
		readonly ChangeEventHelper ChangeEventHelper;
		readonly ResumableLoop Loop;

		public bool IsDbgDisabled; // (disable this adorner for debugging purposes)


		public PatternHighlightsAdorner( MyRichTextBox rtb, ChangeEventHelper ceh ) : base( rtb )
		{
			ChangeEventHelper = ceh;
			Debug.Assert( ChangeEventHelper != null );

			Rtb.TextChanged += Rtb_TextChanged;
			Rtb.AddHandler( ScrollViewer.ScrollChangedEvent, new RoutedEventHandler( Rtb_ScrollChanged ), true );

			Loop = new ResumableLoop( ThreadProc, 33, 33, 444 );
		}


		MyRichTextBox Rtb
		{
			get { return (MyRichTextBox)AdornedElement; }
		}


		private void Rtb_TextChanged( object sender, TextChangedEventArgs e )
		{
			if( IsDbgDisabled ) return;
			if( ChangeEventHelper == null || ChangeEventHelper.IsInChange ) return;

			//........
		}


		private void Rtb_ScrollChanged( object sender, RoutedEventArgs e )
		{
			if( IsDbgDisabled ) return;
			if( ChangeEventHelper == null || ChangeEventHelper.IsInChange ) return;

			InvalidateVisual( ); // to redraw what we already have, in new positions
			Loop.SendRestart( );
		}


		protected override void OnRender( DrawingContext drawingContext )
		{
			base.OnRender( drawingContext );  // (probably nothing)

			if( IsDbgDisabled ) return;

			var dc = drawingContext;
			var rtb = Rtb;
			var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );

			dc.PushClip( new RectangleGeometry( clip_rect ) );

			var t = new TranslateTransform( -rtb.HorizontalOffset, -rtb.VerticalOffset );
			dc.PushTransform( t );


			//......
			//dc.DrawLine( new Pen( Brushes.Red, 1 ), new Point( 0, 0 ), new Point( 100, 100 ) );


			dc.Pop( ); // (transform)
			dc.Pop( ); // (clip)
		}


		protected override void OnRenderSizeChanged( SizeChangedInfo sizeInfo )
		{
			base.OnRenderSizeChanged( sizeInfo );

			if( IsDbgDisabled ) return;

			DelayedInvalidateVisual( );
			Loop.SendRestart( );
		}


		void DelayedInvalidateVisual( )
		{
			Dispatcher.BeginInvoke( DispatcherPriority.Background, new Action( InvalidateVisual ) );
		}


		void ThreadProc( ICancellable cnc )
		{
			//.........
		}
	}
}
