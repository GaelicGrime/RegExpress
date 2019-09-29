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

		static readonly char[] Characters = { ' ', '\t', '\r', '\n' }; // Note. For performance reasons, we only consider regular spaces

		bool mShowWhitespaces = false;


		public WhitespaceAdorner( UIElement adornedElement ) : base( adornedElement )
		{
			Debug.Assert( adornedElement is MyRichTextBox );

			WsBrush.Freeze( );
			TabPen.Freeze( );
			EolPen.Freeze( );
			EofPen.Freeze( );
			EofBrush.Freeze( );

			IsHitTestVisible = false;

			Rtb.TextChanged += Rtb_TextChanged;
			Rtb.AddHandler( ScrollViewer.ScrollChangedEvent, new RoutedEventHandler( Rtb_ScrollChanged ), true );
		}


		public void ShowWhitespaces( bool yes )
		{
			mShowWhitespaces = yes;

			DelayedInvalidateVisual( );
		}


		MyRichTextBox Rtb
		{
			get { return (MyRichTextBox)AdornedElement; }
		}


		private void Rtb_TextChanged( object sender, TextChangedEventArgs e )
		{
			DelayedInvalidateVisual( );
		}


		private void Rtb_ScrollChanged( object sender, RoutedEventArgs e )
		{
			InvalidateVisual( );
		}


		protected override void OnRender( DrawingContext drawingContext )
		{
			base.OnRender( drawingContext );  // (probably nothing)

			if( mShowWhitespaces )
			{
				var dc = drawingContext;
				var rtb = Rtb;
				var td = rtb.GetTextData( null );

				ShowSpacesTabsAndEols( dc, td );
				ShowEof( dc, td );
			}
		}


		protected override void OnRenderSizeChanged( SizeChangedInfo sizeInfo )
		{
			base.OnRenderSizeChanged( sizeInfo );

			DelayedInvalidateVisual( );
		}


		void DelayedInvalidateVisual( )
		{
			Dispatcher.BeginInvoke( DispatcherPriority.Background, new Action( InvalidateVisual ) );
		}


		void ShowSpacesTabsAndEols( DrawingContext dc, TextData td )
		{
			var rtb = Rtb;
			var start_doc = Rtb.Document.ContentStart;
			var end_doc = Rtb.Document.ContentStart;

			if( !start_doc.HasValidLayout || !end_doc.HasValidLayout ) return;
			if( !td.Pointers[0].IsInSameDocument( start_doc ) ) return;

			var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );
			dc.PushClip( new RectangleGeometry( clip_rect ) );

			TextPointer start_pointer = rtb.GetPositionFromPoint( new Point( 0, 0 ), true );
			TextPointer end_pointer = rtb.GetPositionFromPoint( new Point( rtb.ViewportWidth, rtb.ViewportHeight ), true );

			start_pointer = start_pointer.GetInsertionPosition( LogicalDirection.Forward );
			end_pointer = end_pointer.GetInsertionPosition( LogicalDirection.Backward );

			int start_i = RtbUtilities.FindNearestBefore( td.Pointers, start_pointer );
			int end_i = RtbUtilities.FindNearestAfter( td.Pointers, end_pointer );

			if( start_i < 0 ) start_i = 0;
			Debug.Assert( end_i >= 0 );
			if( end_i < 0 ) end_i = td.Pointers.Count - 1;

			for( var i = td.Text.IndexOfAny( Characters, start_i ); i >= 0 && i <= end_i; i = td.Text.IndexOfAny( Characters, i + 1 ) )
			{
				var left = td.Pointers[i];
				var right = td.Pointers[i + 1];

				var rect_left = left.GetCharacterRect( LogicalDirection.Forward );
				var rect_right = right.GetCharacterRect( LogicalDirection.Backward );

				switch( td.Text[i] )
				{
					case '\t':
					{
						const int ARROW_WIDTH = 6;

						var rect = rect_left;
						rect.Offset( 2, 0 );

						var x = Math.Ceiling( rect.Left ) + TabPen.Thickness / 2;
						var y = Math.Ceiling( rect.Top + rect.Height / 2 ) - TabPen.Thickness / 2;

						dc.DrawLine( TabPen, new Point( x, y ), new Point( x + ARROW_WIDTH, y ) );
						dc.DrawLine( TabPen, new Point( x + ARROW_WIDTH / 2, y - ARROW_WIDTH / 2 ), new Point( x + ARROW_WIDTH, y ) );
						dc.DrawLine( TabPen, new Point( x + ARROW_WIDTH / 2, y + ARROW_WIDTH / 2 ), new Point( x + ARROW_WIDTH, y ) );
					}
					break;

					case '\r':
					case '\n':
					{
						const int EOL_WIDTH = 6;

						var rect = rect_left;
						rect.Offset( 2, 0 );

						var x = Math.Ceiling( rect.Left ) + EolPen.Thickness / 2;
						var y = Math.Ceiling( rect.Top + rect.Height / 2 ) - EolPen.Thickness / 2;

						dc.DrawLine( EolPen, new Point( x, y ), new Point( x + EOL_WIDTH, y ) );
						dc.DrawLine( EolPen, new Point( x + EOL_WIDTH, y ), new Point( x + EOL_WIDTH, y - rect.Height * 0.45 ) );
						dc.DrawLine( EolPen, new Point( x, y ), new Point( x + EOL_WIDTH / 2, y - EOL_WIDTH / 2 ) );
						dc.DrawLine( EolPen, new Point( x, y ), new Point( x + EOL_WIDTH / 2, y + EOL_WIDTH / 2 ) );
					}
					break;

					default: // (space)
					{
						const int DOT_SIZE = 2;

						var rect = new Rect( rect_left.TopLeft, rect_right.BottomRight );
						var x = rect.Left + rect.Width / 2;
						var y = Math.Floor( rect.Top + rect.Height / 2 - DOT_SIZE / 2 + 0.5 );
						var dot_rect = new Rect( x, y, DOT_SIZE, DOT_SIZE );

						dc.DrawRectangle( WsBrush, null, dot_rect );
					}
					break;
				}

			}

			dc.Pop( );
		}


		void ShowEof( DrawingContext dc, TextData td )
		{
			var rtb = Rtb;

			if( !rtb.Document.ContentEnd.HasValidLayout ) return;

			var rect = rtb.Document.ContentEnd.GetCharacterRect( LogicalDirection.Forward ); // (no width)
			if( rect.IsEmpty ) return;

			var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );

			dc.PushClip( new RectangleGeometry( clip_rect ) );

			const double EOF_WIDTH = 4;
			double h = Math.Ceiling( rect.Height * 0.3 );

			var x = Math.Ceiling( rect.Left + 3 ) + EofPen.Thickness / 2;
			var y = Math.Floor( rect.Top + ( rect.Height - h ) / 2 ) - EofPen.Thickness / 2;

			var eof_rect = new Rect( x, y, EOF_WIDTH, h );

			dc.DrawRectangle( EofBrush, EofPen, eof_rect );

			dc.Pop( );
		}
	}
}

