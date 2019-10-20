﻿using RegExpressWPF.Code;
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

		const string LinesWithNotBasicRtl = @"
			(?<=
			   ((?'b'^) | (?'e'(?>\r\n|\n\r|\r|\n)))
			   [^\r\n\p{IsHebrew}\p{IsArabic}]*?
			)
			(?(b)(\r\n|\n\r|\r|\n)|\k<e>)
			";

		const string LinesWithBasicRtl = @"
			(?<=
			   ((?'b'^)|(?'e'(?>\r\n|\n\r|\r|\n)))
			   ([^\r\n]*?[\p{IsHebrew}\p{IsArabic}][^\r\n]*?)
			)
			(?(b)(\r\n|\n\r|\r|\n)|\k<e>)
			";

		readonly Regex LinesWithNotBasicRtlRegex = new Regex( LinesWithNotBasicRtl, RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace );
		readonly Regex LinesWithBasicRtlRegex = new Regex( LinesWithBasicRtl, RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace );
		readonly Regex AnyBasicRtlRegex = new Regex( @"[\p{IsHebrew}\p{IsArabic}]", RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace );

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


		void Invoke( CancellationToken ct, Action action )
		{
			Dispatcher.Invoke( action, DispatcherPriority.Background, ct );
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

			rect.Offset( 2, 0 );

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
			try
			{
				if( ct.WaitHandle.WaitOne( 11 ) ) return;
				ct.ThrowIfCancellationRequested( );

				var rtb = Rtb;
				TextData td = null;
				Rect clip_rect = Rect.Empty;
				int start_i = 0;

				Invoke( ct,
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
					} );

				if( td == null ) return;

				ct.ThrowIfCancellationRequested( );

				// spaces and tabs
				{
					List<Rect> positions_spaces = new List<Rect>( );
					List<Rect> positions_tabs = new List<Rect>( );

					List<int> indices = new List<int>( );

					for( var i = td.Text.IndexOfAny( SpacesAndTabs, start_i );
						i >= 0;
						i = td.Text.IndexOfAny( SpacesAndTabs, i + 1 ) )
					{
						ct.ThrowIfCancellationRequested( );

						indices.Add( i );
					}

					var intermediate_results1 = new List<(int index, Rect left, Rect right)>( );
					var intermediate_results2 = new List<(int index, Rect left, Rect right)>( );
					var intermediate_results = intermediate_results1;
					int current_i = 0;

					void do_things( )
					{
						var end_time = Environment.TickCount + 22;
						do
						{
							if( current_i >= indices.Count ) break;
							//if( ct.IsCancellationRequested ) break; -- not possible

							var index = indices[current_i];
							var left = td.Pointers[index];
							var right = td.Pointers[index + 1];

							var rect_left = left.GetCharacterRect( LogicalDirection.Forward );
							var rect_right = right.GetCharacterRect( LogicalDirection.Backward );

							intermediate_results.Add( (index, rect_left, rect_right) );

							++current_i;

						} while( Environment.TickCount < end_time );
					}

					var d = Dispatcher.InvokeAsync( do_things, DispatcherPriority.Background, ct );

					for(; ; )
					{
						d.Task.Wait( ct );

						ct.ThrowIfCancellationRequested( );

						(intermediate_results1, intermediate_results2) = (intermediate_results2, intermediate_results1);
						intermediate_results = intermediate_results1;

						if( !intermediate_results2.Any( ) ) break;

						d = Dispatcher.InvokeAsync( do_things, DispatcherPriority.Background, ct );

						bool should_break = false;

						foreach( var (index, left_rect, right_rect) in intermediate_results2 )
						{
							ct.ThrowIfCancellationRequested( );

							if( right_rect.Bottom < clip_rect.Top ) continue;
							if( left_rect.Top > clip_rect.Bottom )
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

				ct.ThrowIfCancellationRequested( );

				// end-of-lines
				{
					List<Rect> positions_eols = new List<Rect>( );

					// TODO: reduce 'td.Text' to limit the search area

					// lines with no right-to-left segments

					foreach( Match m in LinesWithNotBasicRtlRegex.Matches( td.Text, start_i ) )
					{
						ct.ThrowIfCancellationRequested( );

						TextPointer left = td.Pointers[m.Index];
						Rect eol_rect = Rect.Empty;

						var d = Dispatcher.InvokeAsync(
							( ) =>
							{
								eol_rect = left.GetCharacterRect( LogicalDirection.Forward );
							}, DispatcherPriority.Background, ct );

						d.Task.Wait( ct ); //

						if( eol_rect.Bottom < clip_rect.Top ) continue;
						if( eol_rect.Top > clip_rect.Bottom ) break;

						eol_rect.Offset( rtb.HorizontalOffset, rtb.VerticalOffset );

						positions_eols.Add( eol_rect );
					}

					ct.ThrowIfCancellationRequested( );

					// lines with right-to-left segments; 
					// need additional navigation to find the right-most X

					foreach( Match m in LinesWithBasicRtlRegex.Matches( td.Text, start_i ) )
					{
						ct.ThrowIfCancellationRequested( );

						TextPointer left = td.Pointers[m.Index];
						Rect left_rect = Rect.Empty;
						double max_x = double.NaN;

						bool should_continue = false;
						bool should_break = false;

						var d = Dispatcher.InvokeAsync(
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
							}, DispatcherPriority.Background, ct );

						d.Task.Wait( ct ); //

						if( should_continue ) continue;
						if( should_break ) break;

						Rect eol_rect = new Rect( new Point( max_x, left_rect.Top ), left_rect.Size );
						eol_rect.Offset( rtb.HorizontalOffset, rtb.VerticalOffset );

						positions_eols.Add( eol_rect );
					}

					lock( this )
					{
						PositionsEols = positions_eols;
					}

					DelayedInvalidateVisual( );
				}

				ct.ThrowIfCancellationRequested( );

				// end-of-file
				{
					double max_x = double.NaN;
					Rect end_rect = Rect.Empty;

					var d = Dispatcher.InvokeAsync(
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
								var t = r.Text;

								if( !AnyBasicRtlRegex.IsMatch( t ) )
								{
									return;
								}
							}

							// we have RTL segments that need additional navigation to find the right-most X

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
						}, DispatcherPriority.Background, ct );

					d.Task.Wait( ct ); //

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
