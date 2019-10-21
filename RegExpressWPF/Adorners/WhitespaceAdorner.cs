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
		Rect PositionEof = Rect.Empty;

		readonly char[] SpacesAndTabs = new[] { ' ', '\t' }; // (For performance reasons, we only consider regular spaces)
		readonly Regex EolRegex = new Regex( @"(?>\r\n|\n\r|\r|\n)", RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace );

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
			if( IsDbgDisabled ) return;

			CollectWhitespacesTask.Stop( );

			mShowWhitespaces = yes;

			if( mShowWhitespaces )
			{
				CollectWhitespacesTask.Restart( CollectWhitespacesTaskProc );
			}
			else
			{
				lock( this )
				{
					PositionsSpaces.Clear( );
					PositionsTabs.Clear( );
					PositionsEols.Clear( );
					PositionEof = Rect.Empty;
				}

				DelayedInvalidateVisual( );
			}
		}


		MyRichTextBox Rtb
		{
			get { return (MyRichTextBox)AdornedElement; }
		}


		private void Rtb_TextChanged( object sender, TextChangedEventArgs e )
		{
			if( IsDbgDisabled ) return;
			if( ChangeEventHelper == null || ChangeEventHelper.IsInChange ) return;
			if( !mShowWhitespaces ) return;

			CollectWhitespacesTask.Cancel( );

			// invalidate some areas

			var rtb = Rtb;

			foreach( var change in e.Changes )
			{
				TextPointer start = rtb.Document.ContentStart.GetPositionAtOffset( change.Offset );
				if( start == null ) continue;

				TextPointer end = start.GetPositionAtOffset( Math.Max( change.RemovedLength, change.AddedLength ) );
				if( end == null ) continue;

				var start_rect = start.GetCharacterRect( LogicalDirection.Forward );
				var end_rect = end.GetCharacterRect( LogicalDirection.Backward );
				var change_rect = Rect.Union( start_rect, end_rect );
				if( change_rect.IsEmpty ) continue;

				//
				change_rect = new Rect( change_rect.Left, change_rect.Top, rtb.ViewportWidth, change_rect.Height );
				change_rect.Offset( rtb.HorizontalOffset, rtb.VerticalOffset );

				lock( this )
				{
					for( int i = 0; i < PositionsSpaces.Count; ++i )
					{
						Rect r = PositionsSpaces[i];
						if( r.IntersectsWith( change_rect ) )
						{
							PositionsSpaces[i] = Rect.Empty;
						}
					}

					for( int i = 0; i < PositionsEols.Count; ++i )
					{
						Rect r = PositionsEols[i];
						if( r.IntersectsWith( change_rect ) )
						{
							PositionsEols[i] = Rect.Empty;
						}
					}

					if( PositionEof.IntersectsWith( change_rect ) )
					{
						PositionEof = Rect.Empty;
					}
				}

				InvalidateVisual( );
			}

			CollectWhitespacesTask.Restart( CollectWhitespacesTaskProc );
		}


		private void Rtb_ScrollChanged( object sender, RoutedEventArgs e )
		{
			if( IsDbgDisabled ) return;
			if( ChangeEventHelper == null || ChangeEventHelper.IsInChange ) return;
			if( !mShowWhitespaces ) return;

			InvalidateVisual( ); // to redraw what we already have, in new positions

			CollectWhitespacesTask.Restart( CollectWhitespacesTaskProc );
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
				if( !rect.IsEmpty ) DrawSpace( dc, rect );
			}

			foreach( var rect in positions_tabs )
			{
				if( !rect.IsEmpty ) DrawTab( dc, rect );
			}

			foreach( var rect in positions_eols )
			{
				if( !rect.IsEmpty ) DrawEol( dc, rect );
			}

			if( !PositionEof.IsEmpty )
			{
				DrawEof( dc, PositionEof );
			}

			dc.Pop( ); // (transform)
			dc.Pop( ); // (clip)
		}


		protected override void OnRenderSizeChanged( SizeChangedInfo sizeInfo )
		{
			base.OnRenderSizeChanged( sizeInfo );

			if( IsDbgDisabled ) return;

			DelayedInvalidateVisual( );
		}


		void DelayedInvalidateVisual( )
		{
			Dispatcher.BeginInvoke( DispatcherPriority.Background, new Action( InvalidateVisual ) );
		}


		void DrawSpace( DrawingContext dc, Rect rect )
		{
			const int DOT_SIZE = 2;

			var x = rect.Left + rect.Width / 2;
			var y = Math.Floor( rect.Top + rect.Height / 2 - DOT_SIZE / 2 );
			var dot_rect = new Rect( x, y, DOT_SIZE, DOT_SIZE );

			dc.DrawRectangle( WsBrush, null, dot_rect );
		}


		void DrawTab( DrawingContext dc, Rect rect )
		{
			const int ARROW_WIDTH = 6;

			var half_pen = TabPen.Thickness / 2;

			var x = Math.Ceiling( rect.Left ) + half_pen;
			var y = Math.Ceiling( rect.Top + rect.Height / 2 ) - half_pen;

			dc.DrawLine( TabPen, new Point( x, y ), new Point( x + ARROW_WIDTH, y ) );
			dc.DrawLine( TabPen, new Point( x + ARROW_WIDTH / 2, y - ARROW_WIDTH / 2 ), new Point( x + ARROW_WIDTH, y ) );
			dc.DrawLine( TabPen, new Point( x + ARROW_WIDTH / 2, y + ARROW_WIDTH / 2 ), new Point( x + ARROW_WIDTH, y ) );
		}


		void DrawEol( DrawingContext dc, Rect eol_rect )
		{
			const int EOL_WIDTH = 6;

			var half_pen = EolPen.Thickness / 2;

			var x = Math.Ceiling( eol_rect.Left + 2 ) + half_pen;
			var y = Math.Ceiling( eol_rect.Top + eol_rect.Height / 2 ) - half_pen;

			dc.DrawLine( EolPen, new Point( x, y ), new Point( x + EOL_WIDTH, y ) );
			dc.DrawLine( EolPen, new Point( x + EOL_WIDTH, y ), new Point( x + EOL_WIDTH, y - eol_rect.Height * 0.35 ) );
			dc.DrawLine( EolPen, new Point( x, y ), new Point( x + EOL_WIDTH / 2, y - EOL_WIDTH / 2 ) );
			dc.DrawLine( EolPen, new Point( x, y ), new Point( x + EOL_WIDTH / 2, y + EOL_WIDTH / 2 ) );
		}


		void DrawEof( DrawingContext dc, Rect rect )
		{
			const double EOF_WIDTH = 4;

			double h = Math.Ceiling( rect.Height * 0.3 );
			double half_pen = EofPen.Thickness / 2;

			var x = Math.Ceiling( rect.Left + 2 ) + half_pen;
			var y = Math.Floor( rect.Top + ( rect.Height - h ) / 2 ) - half_pen;

			var eof_rect = new Rect( x, y, EOF_WIDTH, h );

			dc.DrawRectangle( EofBrush, EofPen, eof_rect );
		}


		[System.Diagnostics.CodeAnalysis.SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void CollectWhitespacesTaskProc( CancellationToken ct )
		{
			Debug.Assert( !Rtb.Dispatcher.CheckAccess( ) ); // supposed to be done on non-UI thread

			try
			{
				if( ct.WaitHandle.WaitOne( 33 ) ) return;
				ct.ThrowIfCancellationRequested( );

				var rtb = Rtb;
				TextData td = null;
				Rect clip_rect = Rect.Empty;
				int top_index = 0;

				UITaskHelper.Invoke( ct,
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
						top_index = RtbUtilities.FindNearestBefore( td.Pointers, start_pointer );
						if( top_index < 0 ) top_index = 0;
					} );

				if( td == null ) return;

				CollectEols( ct, td, clip_rect, top_index );
				CollectEof( ct, td, clip_rect, top_index );
				CollectSpaces( ct, td, clip_rect, top_index );
			}
			catch( OperationCanceledException exc ) // also 'TaskCanceledException'
			{
				Utilities.DbgSimpleLog( exc );

				// ignore
			}
			catch( Exception exc )
			{
				_ = exc;
				if( Debugger.IsAttached ) Debugger.Break( );
				throw;
			}
		}


		void CollectSpaces( CancellationToken ct, TextData td, Rect clipRect, int topIndex )
		{
			ct.ThrowIfCancellationRequested( );

			var rtb = Rtb;

			List<Rect> positions_spaces = new List<Rect>( );
			List<Rect> positions_tabs = new List<Rect>( );

			List<int> indices = new List<int>( );

			for( var i = td.Text.IndexOfAny( SpacesAndTabs, topIndex );
				i >= 0;
				i = td.Text.IndexOfAny( SpacesAndTabs, i + 1 ) )
			{
				ct.ThrowIfCancellationRequested( );

				indices.Add( i );
			}

			var intermediate_results1 = new List<(int index, Rect left, Rect right)>( );
			var intermediate_results2 = new List<(int index, Rect left, Rect right)>( );
			int current_i = 0;

			void do_things( )
			{
				Debug.Assert( !intermediate_results1.Any( ) );

				var end_time = Environment.TickCount + 22;
				do
				{
					if( current_i >= indices.Count ) break;
					//if( ct.IsCancellationRequested ) break; -- not possible

					var index = indices[current_i];
					var left = td.Pointers[index];
					var right = td.Pointers[index + 1];

					var left_rect = left.GetCharacterRect( LogicalDirection.Forward );
					var right_rect = right.GetCharacterRect( LogicalDirection.Backward );

					intermediate_results1.Add( (index, left_rect, right_rect) );

					if( left_rect.Top > clipRect.Bottom ) break;

					++current_i;

				} while( Environment.TickCount < end_time );
			}

			var d = UITaskHelper.BeginInvoke( ct, do_things );

			for(; ; )
			{
				d.Wait( ct );

				ct.ThrowIfCancellationRequested( );

				(intermediate_results1, intermediate_results2) = (intermediate_results2, intermediate_results1);

				if( !intermediate_results2.Any( ) ) break;

				d = UITaskHelper.BeginInvoke( ct, do_things );

				bool should_break = false;

				Debug.Assert( !Rtb.Dispatcher.CheckAccess( ) );

				foreach( var (index, left_rect, right_rect) in intermediate_results2 )
				{
					ct.ThrowIfCancellationRequested( );

					if( right_rect.Bottom < clipRect.Top ) continue;
					if( left_rect.Top > clipRect.Bottom )
					{
						should_break = true;
						break;
					}

					switch( td.Text[index] )
					{
					case '\t':
						positions_tabs.Add( Rect.Offset( left_rect, rtb.HorizontalOffset, rtb.VerticalOffset ) );
						break;

					default: // (space)
						var r = new Rect( left_rect.TopLeft, right_rect.BottomRight );
						r.Offset( rtb.HorizontalOffset, rtb.VerticalOffset );
						positions_spaces.Add( r );
						break;
					}
				}

				if( should_break ) break;

				intermediate_results2.Clear( );
			}

			lock( this )
			{
				PositionsSpaces = positions_spaces;
				PositionsTabs = positions_tabs;
			}

			DelayedInvalidateVisual( );
		}


		void CollectEols( CancellationToken ct, TextData td, Rect clip_rect, int top_index )
		{
			ct.ThrowIfCancellationRequested( );

			var rtb = Rtb;

			List<Rect> positions_eols = new List<Rect>( );

			// lines with no right-to-left segments

			var matches = EolRegex.Matches( td.Text );

			for( int i = 0; i < matches.Count; ++i )
			{
				ct.ThrowIfCancellationRequested( );

				int index = matches[i].Index;

				if( index < top_index ) continue;

				int previous_index = i == 0 ? 0 : matches[i - 1].Index;

				bool has_RTL = false;

				for( int k = previous_index; k < index; ++k )
				{
					ct.ThrowIfCancellationRequested( );

					if( UnicodeUtilities.IsRTL( td.Text[k] ) )
					{
						has_RTL = true;
						break;
					}
				}

				if( has_RTL )
				{
					// RTL needs more navigation to find the rightmost X

					TextPointer left = td.Pointers[index];
					Rect left_rect = Rect.Empty;
					double max_x = double.NaN;

					bool should_continue = false;
					bool should_break = false;

					UITaskHelper.Invoke( ct,
						( ) =>
						{
							left_rect = left.GetCharacterRect( LogicalDirection.Forward );

							if( left_rect.Bottom < clip_rect.Top ) { should_continue = true; return; }
							if( left_rect.Top > clip_rect.Bottom ) { should_break = true; return; }

							max_x = left_rect.Left;

							for( var tp = left.GetInsertionPosition( LogicalDirection.Backward ); ; )
							{
								tp = tp.GetNextInsertionPosition( LogicalDirection.Backward );
								if( tp == null ) break;

								// WORKAROUND for lines like "0ראל", when "0" is matched and highlighted
								tp = tp.GetInsertionPosition( LogicalDirection.Forward );

								var rect_b = tp.GetCharacterRect( LogicalDirection.Backward );
								var rect_f = tp.GetCharacterRect( LogicalDirection.Forward );

								if( rect_b.Bottom < left_rect.Top && rect_f.Bottom < left_rect.Top ) break;

								if( rect_b.Bottom > left_rect.Top )
								{
									if( max_x < rect_b.Left ) max_x = rect_b.Left;
								}

								if( rect_f.Bottom > left_rect.Top )
								{
									if( max_x < rect_f.Left ) max_x = rect_f.Left;
								}
							}
						} );

					if( should_continue ) continue;
					if( should_break ) break;

					Rect eol_rect = new Rect( new Point( max_x, left_rect.Top ), left_rect.Size );
					eol_rect.Offset( rtb.HorizontalOffset, rtb.VerticalOffset );

					positions_eols.Add( eol_rect );
				}
				else
				{
					// no RTL; quick answer

					TextPointer left = td.Pointers[index];
					Rect eol_rect = Rect.Empty;

					UITaskHelper.Invoke( ct,
						( ) =>
						{
							eol_rect = left.GetCharacterRect( LogicalDirection.Forward );
						} );

					if( eol_rect.Bottom < clip_rect.Top ) continue;
					if( eol_rect.Top > clip_rect.Bottom ) break;

					eol_rect.Offset( rtb.HorizontalOffset, rtb.VerticalOffset );

					positions_eols.Add( eol_rect );
				}
			}

			lock( this )
			{
				PositionsEols = positions_eols;
			}

			DelayedInvalidateVisual( );
		}


		void CollectEof( CancellationToken ct, TextData td, Rect clip_rect, int top_index )
		{
			ct.ThrowIfCancellationRequested( );

			var rtb = Rtb;

			double max_x = double.NaN;
			Rect end_rect = Rect.Empty;

			UITaskHelper.Invoke( ct,
				( ) =>
				{
					var end = rtb.Document.ContentEnd;
					end_rect = end.GetCharacterRect( LogicalDirection.Forward ); // (no width)

					if( end_rect.Bottom < clip_rect.Top || end_rect.Top > clip_rect.Bottom ) return;

					max_x = end_rect.Left;

					// if no RTL, then return a quick answer

					var begin_line = end.GetLineStartPosition( 0 );
					if( begin_line != null )
					{
						var r = new TextRange( begin_line, end );
						var text = r.Text;
						bool has_RTL = false;

						for( int k = 0; k < text.Length; ++k )
						{
							ct.ThrowIfCancellationRequested( );

							if( UnicodeUtilities.IsRTL( text[k] ) )
							{
								has_RTL = true;
								break;
							}
						}

						if( !has_RTL )
						{
							return;
						}
					}

					// we have RTL segments that need additional navigation to find the rightmost X

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
				} );

			lock( this )
			{
				if( double.IsNaN( max_x ) )
				{
					PositionEof = Rect.Empty;
				}
				else
				{
					PositionEof = new Rect( new Point( max_x, end_rect.Top ), end_rect.Size );
					PositionEof.Offset( rtb.HorizontalOffset, rtb.VerticalOffset );
				}
			}

			DelayedInvalidateVisual( );

		}
	}
}
