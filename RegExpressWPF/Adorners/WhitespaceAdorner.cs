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

		public bool IsDbgDisabled; // (disable this adorner for debugging purposes)


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


		public void ShowWhiteSpaces( bool yes )
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

			if( IsDbgDisabled ) return;

			if( mShowWhitespaces )
			{
				var dc = drawingContext;
				var rtb = Rtb;
				var td = rtb.GetTextData( null );

				ShowSpacesTabsAndEols( dc, td );
				ShowEof( dc );
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
			if( !td.Pointers.Any( ) || !td.Pointers[0].IsInSameDocument( start_doc ) ) return;

			var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );
			dc.PushClip( new RectangleGeometry( clip_rect ) );

			TextPointer start_pointer = rtb.GetPositionFromPoint( new Point( 0, 0 ), true ).GetLineStartPosition( -1, out int unused );
			int start_i = RtbUtilities.FindNearestBefore( td.Pointers, start_pointer );

			if( start_i < 0 ) start_i = 0;

			for( var i = td.Text.IndexOfAny( Characters, start_i ); i >= 0; i = td.Text.IndexOfAny( Characters, i + 1 ) )
			{
				var left = td.Pointers[i];
				var right = td.Pointers[i + 1];

				var rect_left = left.GetCharacterRect( LogicalDirection.Forward );
				var rect_right = right.GetCharacterRect( LogicalDirection.Backward );

				if( rect_left.Top > clip_rect.Bottom ) break;

				switch( td.Text[i] )
				{
					case '\t':
					{
						const int ARROW_WIDTH = 6;

						var rect = rect_left;
						rect.Offset( 2, 0 );

						var half_pen = TabPen.Thickness / 2;

						var x = Math.Ceiling( rect.Left ) + half_pen;
						var y = Math.Ceiling( rect.Top + rect.Height / 2 ) - half_pen;

						dc.DrawLine( TabPen, new Point( x, y ), new Point( x + ARROW_WIDTH, y ) );
						dc.DrawLine( TabPen, new Point( x + ARROW_WIDTH / 2, y - ARROW_WIDTH / 2 ), new Point( x + ARROW_WIDTH, y ) );
						dc.DrawLine( TabPen, new Point( x + ARROW_WIDTH / 2, y + ARROW_WIDTH / 2 ), new Point( x + ARROW_WIDTH, y ) );
					}
					break;

					case '\r':
					case '\n':
					{
						DrawEol( dc, left, rect_left );
					}
					break;

					default: // (space)
					{
						const int DOT_SIZE = 2;

						var rect = new Rect( rect_left.TopLeft, rect_right.BottomRight );
						var x = rect.Left + rect.Width / 2;
						var y = Math.Floor( rect.Top + rect.Height / 2 - DOT_SIZE / 2 );
						var dot_rect = new Rect( x, y, DOT_SIZE, DOT_SIZE );

						dc.DrawRectangle( WsBrush, null, dot_rect );
					}
					break;
				}

			}

			dc.Pop( );
		}


		void DrawEol( DrawingContext dc, TextPointer eol_pointer, Rect eol_rect )
		{
			double max_x = eol_rect.Left;

			for( var tp = eol_pointer; ; )
			{
				tp = tp.GetNextInsertionPosition( LogicalDirection.Backward );
				if( tp == null ) break;

				var rect = tp.GetCharacterRect( LogicalDirection.Forward );
				if( rect.Bottom < eol_rect.Bottom ) break;
				if( rect.Left < max_x ) break;

				max_x = rect.Left;
			}

			const int EOL_WIDTH = 6;

			var half_pen = EolPen.Thickness / 2;

			var x = Math.Ceiling( max_x + 2 ) + half_pen;
			var y = Math.Ceiling( eol_rect.Top + eol_rect.Height / 2 ) - half_pen;

			dc.DrawLine( EolPen, new Point( x, y ), new Point( x + EOL_WIDTH, y ) );
			dc.DrawLine( EolPen, new Point( x + EOL_WIDTH, y ), new Point( x + EOL_WIDTH, y - eol_rect.Height * 0.35 ) );
			dc.DrawLine( EolPen, new Point( x, y ), new Point( x + EOL_WIDTH / 2, y - EOL_WIDTH / 2 ) );
			dc.DrawLine( EolPen, new Point( x, y ), new Point( x + EOL_WIDTH / 2, y + EOL_WIDTH / 2 ) );
		}


		void ShowEof( DrawingContext dc )
		{
			var rtb = Rtb;

			if( !rtb.Document.ContentEnd.HasValidLayout ) return;

			var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );

			var end = rtb.Document.ContentEnd;
			var end_rect = end.GetCharacterRect( LogicalDirection.Forward ); // (no width)

			if( end_rect.Bottom < clip_rect.Top || end_rect.Top > clip_rect.Bottom ) return;

			double max_x = end_rect.Left;

			for( var tp = end; ; )
			{
				tp = tp.GetNextInsertionPosition( LogicalDirection.Backward );
				if( tp == null ) break;

				var rect = tp.GetCharacterRect( LogicalDirection.Forward );
				if( rect.Bottom < end_rect.Bottom ) break;
				if( rect.Left < max_x ) break;

				max_x = rect.Left;
			}

			dc.PushClip( new RectangleGeometry( clip_rect ) );

			const double EOF_WIDTH = 4;
			double h = Math.Ceiling( end_rect.Height * 0.3 );
			double half_pen = EofPen.Thickness / 2;

			var x = Math.Ceiling( max_x + 3 ) + half_pen;
			var y = Math.Floor( end_rect.Top + ( end_rect.Height - h ) / 2 ) - half_pen;

			var eof_rect = new Rect( x, y, EOF_WIDTH, h );

			dc.DrawRectangle( EofBrush, EofPen, eof_rect );

			dc.Pop( );
		}
	}
}

