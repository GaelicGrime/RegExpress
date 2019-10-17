using RegExpressWPF.Code;
using RegExpressWPF.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
		readonly ChangeEventHelper ChangeEventHelper;
		readonly Brush WsBrush = Brushes.LightSeaGreen;
		readonly Pen TabPen = new Pen( Brushes.LightSeaGreen, 1 );
		readonly Pen EolPen = new Pen( Brushes.LightSeaGreen, 1 );
		readonly Pen EofPen = new Pen( Brushes.LightSeaGreen, 1 );
		readonly Brush EofBrush = Brushes.Transparent;

		readonly TaskHelper CollectWhitespacesTask = new TaskHelper( );
		List<Rect> PositionsSpaces = new List<Rect>( );
		List<Rect> PositionsTabs = new List<Rect>( );
		List<Rect> PositionsEols = new List<Rect>( );
		Point? PositionEof = null;

		static readonly char[] Characters = { ' ', '\t', '\r', '\n' }; // Note. For performance reasons, we only consider regular spaces

		bool mShowWhitespaces = false;

		public bool IsDbgDisabled; // (disable this adorner for debugging purposes)


		public WhitespaceAdorner( MyRichTextBox rtb, ChangeEventHelper ceh ) : base( rtb )
		{
			ChangeEventHelper = ceh;
			Debug.Assert( ChangeEventHelper != null );

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

			CollectWhitespacesTask.Stop( );

			if( mShowWhitespaces )
			{
				CollectWhitespacesTask.Restart( CollectWhitespacesTaskProc );
			}
			else
			{
				CollectWhitespacesTask.Stop( );
				lock( this )
				{
					PositionsSpaces.Clear( );
					PositionsTabs.Clear( );
					PositionsEols.Clear( );
					PositionEof = null;
				}
			}

			DelayedInvalidateVisual( );
		}


		MyRichTextBox Rtb
		{
			get { return (MyRichTextBox)AdornedElement; }
		}


		private void Rtb_TextChanged( object sender, TextChangedEventArgs e )
		{
			if( ChangeEventHelper == null || ChangeEventHelper.IsInChange ) return;

			//DelayedInvalidateVisual( );

			if( mShowWhitespaces )
			{
				CollectWhitespacesTask.Stop( );

				var rtb = Rtb;

				foreach( var change in e.Changes )
				{
					TextPointer start = rtb.Document.ContentStart.GetPositionAtOffset( change.Offset );
					TextPointer end = start.GetPositionAtOffset( Math.Max( change.RemovedLength, change.AddedLength ) );

					var start_rect = start.GetCharacterRect( LogicalDirection.Forward );
					var end_rect = end.GetCharacterRect( LogicalDirection.Backward );
					var change_rect = Rect.Union( start_rect, end_rect );
					change_rect.Offset( rtb.HorizontalOffset, rtb.VerticalOffset );

					lock( this )
					{
						for( int i = PositionsSpaces.Count - 1; i >= 0; --i )
						{
							Rect r = PositionsSpaces[i];
							if( r.IntersectsWith( change_rect ) )
							{
								PositionsSpaces.RemoveAt( i );
							}
						}
					}

					InvalidateVisual( );
				}

				CollectWhitespacesTask.Restart( CollectWhitespacesTaskProc );
			}
		}


		private void Rtb_ScrollChanged( object sender, RoutedEventArgs e )
		{
			InvalidateVisual( );

			if( mShowWhitespaces )
			{
				CollectWhitespacesTask.Restart( CollectWhitespacesTaskProc );
			}
		}


		protected override void OnRender( DrawingContext drawingContext )
		{
			base.OnRender( drawingContext );  // (probably nothing)

			if( IsDbgDisabled ) return;

			if( !mShowWhitespaces ) return;

			var dc = drawingContext;
			var rtb = Rtb;
			var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );

			dc.PushClip( new RectangleGeometry( clip_rect ) );

			var t = new TranslateTransform( -rtb.HorizontalOffset, -rtb.VerticalOffset );
			dc.PushTransform( t );

			// make copies
			List<Rect> positions_spaces;
			List<Rect> positions_tabs;
			List<Rect> positions_eols;
			lock( this )
			{
				positions_spaces = PositionsSpaces.ToList( );
				positions_tabs = PositionsTabs.ToList( );
				positions_eols = PositionsEols.ToList( );
			}

			foreach( var rect in positions_spaces )
			{
				DrawSpace( dc, rect );
			}

			dc.Pop( );
			dc.Pop( );


			//....

			//if( mShowWhitespaces )
			//{
			//	var dc = drawingContext;
			//	var rtb = Rtb;
			//	var td = rtb.GetTextData( null );

			//	ShowSpacesTabsAndEols( dc, td );
			//	ShowEof( dc );
			//}
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

			TextPointer start_pointer = rtb.GetPositionFromPoint( new Point( 0, 0 ), snapToText: true ).GetLineStartPosition( -1, out int unused );
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
					DrawTab( dc, rect_left );
					break;

				case '\r':
				case '\n':
					DrawEol( dc, left, rect_left );
					break;

				default: // (space)
					DrawSpace( dc, rect_left, rect_right );
					break;
				}

			}

			dc.Pop( );
		}


		void DrawTab( DrawingContext dc, Rect rect )
		{
			const int ARROW_WIDTH = 6;

			rect.Offset( 2, 0 );

			var half_pen = TabPen.Thickness / 2;

			var x = Math.Ceiling( rect.Left ) + half_pen;
			var y = Math.Ceiling( rect.Top + rect.Height / 2 ) - half_pen;

			dc.DrawLine( TabPen, new Point( x, y ), new Point( x + ARROW_WIDTH, y ) );
			dc.DrawLine( TabPen, new Point( x + ARROW_WIDTH / 2, y - ARROW_WIDTH / 2 ), new Point( x + ARROW_WIDTH, y ) );
			dc.DrawLine( TabPen, new Point( x + ARROW_WIDTH / 2, y + ARROW_WIDTH / 2 ), new Point( x + ARROW_WIDTH, y ) );
		}


		void DrawEol( DrawingContext dc, TextPointer eol_pointer, Rect eol_rect )
		{
			double max_x = eol_rect.Left;

			for( var tp = eol_pointer.GetInsertionPosition( LogicalDirection.Backward ); ; )
			{
				tp = tp.GetNextInsertionPosition( LogicalDirection.Backward );
				if( tp == null ) break;

				// WORKAROUND for lines like "0ראל", when "0" is matched and highlighted
				tp = tp.GetInsertionPosition( LogicalDirection.Forward );

				var rect_b = tp.GetCharacterRect( LogicalDirection.Backward );
				var rect_f = tp.GetCharacterRect( LogicalDirection.Forward );

				if( rect_b.Bottom < eol_rect.Top && rect_f.Bottom < eol_rect.Top ) break;

				if( rect_b.Bottom > eol_rect.Top )
				{
					if( max_x < rect_b.Left ) max_x = rect_b.Left;
				}

				if( rect_f.Bottom > eol_rect.Top )
				{
					if( max_x < rect_f.Left ) max_x = rect_f.Left;
				}
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


		//...
		void DrawSpace( DrawingContext dc, Rect rectLeft, Rect rectRight )
		{
			const int DOT_SIZE = 2;

			var rect = new Rect( rectLeft.TopLeft, rectRight.BottomRight );
			var x = rect.Left + rect.Width / 2;
			var y = Math.Floor( rect.Top + rect.Height / 2 - DOT_SIZE / 2 );
			var dot_rect = new Rect( x, y, DOT_SIZE, DOT_SIZE );

			dc.DrawRectangle( WsBrush, null, dot_rect );
		}


		void DrawSpace( DrawingContext dc, Rect rect )
		{
			const int DOT_SIZE = 2;

			var x = rect.Left + rect.Width / 2;
			var y = Math.Floor( rect.Top + rect.Height / 2 - DOT_SIZE / 2 );
			var dot_rect = new Rect( x, y, DOT_SIZE, DOT_SIZE );

			dc.DrawRectangle( WsBrush, null, dot_rect );
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

				// WORKAROUND for lines like "0ראל", when "0" is matched and highlighted
				tp = tp.GetInsertionPosition( LogicalDirection.Forward );

				var rect = tp.GetCharacterRect( LogicalDirection.Forward );
				if( rect.Bottom < end_rect.Bottom ) break;

				if( max_x < rect.Left ) max_x = rect.Left;
			}

			const double EOF_WIDTH = 4;
			double h = Math.Ceiling( end_rect.Height * 0.3 );
			double half_pen = EofPen.Thickness / 2;

			var x = Math.Ceiling( max_x + 2 ) + half_pen;
			var y = Math.Floor( end_rect.Top + ( end_rect.Height - h ) / 2 ) - half_pen;

			var eof_rect = new Rect( x, y, EOF_WIDTH, h );

			dc.PushClip( new RectangleGeometry( clip_rect ) );
			dc.DrawRectangle( EofBrush, EofPen, eof_rect );
			dc.Pop( );
		}


		[System.Diagnostics.CodeAnalysis.SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void CollectWhitespacesTaskProc( CancellationToken ct )
		{
			try
			{
				if( ct.WaitHandle.WaitOne( 11 ) ) return;
				ct.ThrowIfCancellationRequested( );

				List<Rect> positions_spaces = new List<Rect>( );
				List<Rect> positions_tabs = new List<Rect>( );
				List<Rect> positions_eols = new List<Rect>( );

				var rtb = Rtb;
				TextData td = null;
				Rect clip_rect = Rect.Empty;
				int start_i = 0;

				Dispatcher.Invoke(
					( ) =>
					{
						td = null;

						var start_doc = Rtb.Document.ContentStart;
						var end_doc = Rtb.Document.ContentStart;

						if( !start_doc.HasValidLayout || !end_doc.HasValidLayout ) return;

						var td0 = rtb.GetTextData( null );
						if( !td0.Pointers.Any( ) || !td0.Pointers[0].IsInSameDocument( start_doc ) ) return;

						td = td0;
						clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );

						TextPointer start_pointer = rtb.GetPositionFromPoint( new Point( 0, 0 ), snapToText: true ).GetLineStartPosition( -1, out int unused );
						start_i = RtbUtilities.FindNearestBefore( td.Pointers, start_pointer );
						if( start_i < 0 ) start_i = 0;
					},
					DispatcherPriority.Background, ct );
				ct.ThrowIfCancellationRequested( );

				if( td == null ) return;

				for( var i = td.Text.IndexOfAny( Characters, start_i ); i >= 0; i = td.Text.IndexOfAny( Characters, i + 1 ) )
				{
					var left = td.Pointers[i];
					var right = td.Pointers[i + 1];

					Rect rect_left = Rect.Empty;
					Rect rect_right = Rect.Empty;

					Dispatcher.Invoke(
						( ) =>
						{
							rect_left = left.GetCharacterRect( LogicalDirection.Forward );
							rect_right = right.GetCharacterRect( LogicalDirection.Backward );
						},
						DispatcherPriority.Background, ct );

					ct.ThrowIfCancellationRequested( );

					//...
					// continue?
					if( rect_left.Top > clip_rect.Bottom ) break;

					switch( td.Text[i] )
					{
					case '\t':
						positions_tabs.Add( rect_left );
						break;

					case '\r':
					case '\n':
						//...
						break;

					default: // (space)
						var r = new Rect( rect_left.TopLeft, rect_right.BottomRight );
						r.Offset( rtb.HorizontalOffset, rtb.VerticalOffset );
						positions_spaces.Add( r );
						break;
					}
				}

				lock( this )
				{
					PositionsSpaces = positions_spaces;
					PositionsTabs = positions_tabs;
					PositionsEols = positions_eols;
					PositionEof = null; // ...
				}

				DelayedInvalidateVisual( );
			}
			catch( OperationCanceledException ) // also 'TaskCanceledException'
			{
				// ignore
			}
			catch( Exception exc )
			{
				_ = exc;
				throw;
			}
		}
	}
}

