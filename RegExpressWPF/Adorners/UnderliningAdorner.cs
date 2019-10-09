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

		public bool IsDbgDisabled; // (disable drawing this adorner for debugging purposes)


		public UnderliningAdorner( UIElement adornedElement ) : base( adornedElement )
		{
			Debug.Assert( adornedElement is RichTextBox );

			Pen.Freeze( );

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
			DelayedInvalidateVisual( );
		}


		private void Rtb_ScrollChanged( object sender, RoutedEventArgs e )
		{
			InvalidateVisual( );
		}


#if true //.............

		protected override void OnRender( DrawingContext drawingContext )
		{
			base.OnRender( drawingContext );  // (probably nothing)

			if( IsDbgDisabled ) return;

			var ranges = Ranges; // (no locking needed)

			if( ranges == null ) return;

			var dc = drawingContext;
			var rtb = Rtb;

			var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );
			dc.PushClip( new RectangleGeometry( clip_rect ) );


			var start_doc = rtb.Document.ContentStart;
			var half_pen = Pen.Thickness / 2;

			// TODO: clean 'Ranges' if document was changed (release old document), in thread-safe manner

			foreach( var (start, end) in ranges )
			{
				if( start.HasValidLayout && end.HasValidLayout )
				{
					if( start.IsInSameDocument( start_doc ) && end.IsInSameDocument( start_doc ) )
					{
						Point start_point_b = start.GetCharacterRect( LogicalDirection.Backward ).BottomLeft;
						Point start_point_f = start.GetCharacterRect( LogicalDirection.Forward ).BottomLeft;

						TextPointer end_b = end.GetInsertionPosition( LogicalDirection.Backward );
						TextPointer end_f = end.GetInsertionPosition( LogicalDirection.Forward );

						Point end_point_b = end.GetCharacterRect( LogicalDirection.Backward ).BottomLeft;
						Point end_point_f = end.GetCharacterRect( LogicalDirection.Forward ).BottomLeft;

						if( start_point_b.Y <= clip_rect.Bottom && start_point_f.Y <= clip_rect.Bottom &&
							end_point_b.Y >= clip_rect.Top && end_point_f.Y >= clip_rect.Top )
						{
							Point prev_point_b = start_point_b;
							Point prev_point_f = start_point_f;

							TextPointer end_max = end_point_b.X > end_point_f.X ? end_b : end_f;
							TextPointer end_min = end_point_b.X > end_point_f.X ? end_b : end_f;

							int start_offset = start_doc.GetOffsetToPosition( start );
							int end_offset = start_doc.GetOffsetToPosition( end );

							for( 
								var tp = start.GetNextInsertionPosition( LogicalDirection.Forward );
								tp != null && tp.CompareTo( end ) <= 0;
								tp = tp.GetNextInsertionPosition( LogicalDirection.Forward ) 
								)
							{
								int offset = start_doc.GetOffsetToPosition( tp );

								Point tp_point_b = tp.GetCharacterRect( LogicalDirection.Backward ).BottomLeft;
								Point tp_point_f = tp.GetCharacterRect( LogicalDirection.Forward ).BottomLeft;

								Point prev_min, prev_max;
								Point tp_min, tp_max;

								if( prev_point_b.X <= prev_point_f.X )
								{
									prev_min = prev_point_b;
									prev_max = prev_point_f;
								}
								else
								{
									prev_min = prev_point_f;
									prev_max = prev_point_b;
								}

								if( tp_point_b.X <= tp_point_f.X )
								{
									tp_min = tp_point_b;
									tp_max = tp_point_f;
								}
								else
								{
									tp_min = tp_point_f;
									tp_max = tp_point_b;
								}

								Point a, b;

								if( prev_max.Y == tp_min.Y )
								{
									a = prev_max;
									b = tp_min;
								}
								else if( prev_max.Y == tp_max.Y )
								{
									a = prev_max;
									b = tp_max;
								}
								else
								{
									a = prev_min;
									b = tp_min;
								}

								if( a.Y != b.Y )
								{
									// this happens when the text is just edited (e.g. pressing <Enter>);
									// do nothing; another update will reflect new state
								}
								else
								{
									dc.DrawLine( Pen, a, b );
								}

								prev_point_b = tp_point_b;
								prev_point_f = tp_point_f;
							}
						}
					}
				}
			}


			dc.Pop( );
		}


#else

		protected override void OnRender( DrawingContext drawingContext )
		{
			base.OnRender( drawingContext );  // (probably nothing)

			var dc = drawingContext;
			var rtb = Rtb;
			var ranges = Ranges;

			var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );
			dc.PushClip( new RectangleGeometry( clip_rect ) );

			if( ranges != null )
			{
				var start_doc = rtb.Document.ContentStart;

				// TODO: clean 'Ranges' if document was changed (release old document)

				foreach( var (start, end) in ranges )
				{
					if( start.HasValidLayout && end.HasValidLayout )
					{
						if( start.IsInSameDocument( start_doc ) && end.IsInSameDocument( start_doc ) )
						{
							var start_rect = start.GetCharacterRect( LogicalDirection.Forward );
							var end_rect = end.GetCharacterRect( LogicalDirection.Backward );

							if( !( end_rect.Bottom < clip_rect.Top || start_rect.Top > clip_rect.Bottom ) )
							{
								if( start_rect.Bottom > end_rect.Top )
								{
									// no wrap, draw quickly

									DrawUnderline( dc, new Rect( start_rect.TopLeft, end_rect.BottomLeft ), isLeftStart: true, isRightEnd: true );
								}
								else
								{
									var adjusted_end = end.GetInsertionPosition( LogicalDirection.Backward );
									Debug.Assert( adjusted_end != null );
									Debug.Assert( adjusted_end.IsAtInsertionPosition );

									TextPointer tp = start;
									if( !tp.IsAtInsertionPosition ) tp = tp.GetInsertionPosition( LogicalDirection.Forward );
									Debug.Assert( tp.IsAtInsertionPosition );

									RectInfo rect_info = new RectInfo { nextRect = start_rect };

									bool is_left_start = true;

									for(; ; )
									{
										Rect r = Rect.Empty;

										for(; ; )
										{
											rect_info = GetRectInfo( tp, rect_info.nextRect );
											r.Union( rect_info.thisRect );

											if( rect_info.nextPointer == null || !IsBefore( rect_info.nextPointer, adjusted_end ) ) { tp = null; break; }
											tp = rect_info.nextPointer;
											if( !rect_info.isNextOnSameLine ) { break; }
										}

										DrawUnderline( dc, r, is_left_start, tp == null );

										if( tp == null ) break;

										is_left_start = false;
									}
								}
							}
						}
					}
				}
			}

			dc.Pop( );
		}


		void DrawUnderline( DrawingContext dc, Rect rect, bool isLeftStart, bool isRightEnd )
		{
			/*
              
            Too academic and does not look great: 

            var geo = new StreamGeometry( );

            using( var ctx = geo.Open( ) )
            {
                if( isLeftStart )
                {
                    ctx.BeginFigure( rect.BottomLeft + new Vector( 0, -3 ), isFilled: false, isClosed: false );
                    ctx.LineTo( rect.BottomLeft, isStroked: true, isSmoothJoin: true );
                }
                else
                {
                    ctx.BeginFigure( rect.BottomLeft, true, false );
                }

                ctx.LineTo( rect.BottomRight, isStroked: true, isSmoothJoin: true );

                if( isRightEnd )
                {
                    ctx.LineTo( rect.BottomRight + new Vector( 0, -3 ), isStroked: true, isSmoothJoin: true );
                }
            }

            geo.Freeze( );

            dc.DrawGeometry( null, Pen, geo );

            */

			// "Worse is better":

			var half_pen = Pen.Thickness / 2;

			var x_left = Math.Ceiling( rect.Left ) - half_pen;
			var x_right = Math.Ceiling( rect.Right ) - half_pen;
			var y_bottom = Math.Ceiling( rect.Bottom ) - half_pen;
			var y_top = y_bottom - 3;

			dc.DrawLine( Pen, new Point( x_left, y_bottom ), new Point( x_right, y_bottom ) );

			if( isLeftStart )
			{
				dc.DrawLine( Pen, new Point( x_left, y_bottom ), new Point( x_left, y_top ) );
			}

			if( isRightEnd )
			{
				dc.DrawLine( Pen, new Point( x_right, y_bottom ), new Point( x_right, y_top ) );
			}
		}


		struct RectInfo
		{
			public Rect thisRect;
			public TextPointer nextPointer;
			public Rect nextRect;
			public bool isNextOnSameLine;
		}


		static RectInfo GetRectInfo( TextPointer thisPointer, Rect thisLeadingRect )
		{
			Debug.Assert( thisPointer.IsAtInsertionPosition );
			var nextPointer = thisPointer.GetNextInsertionPosition( LogicalDirection.Forward );

			if( nextPointer == null )
			{
				return new RectInfo
				{
					thisRect = new Rect( thisLeadingRect.TopLeft, new Size( 0, thisLeadingRect.Height ) ),
					nextPointer = null,
					nextRect = Rect.Empty,
					isNextOnSameLine = false
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
					isNextOnSameLine = true
				};
			}
			else
			{
				char[] c = new char[1];
				int n = thisPointer.GetTextInRun( LogicalDirection.Forward, c, 0, 1 );

				return new RectInfo
				{
					// TODO: avoid hardcoded width
					thisRect = new Rect( thisLeadingRect.TopLeft, new Size( n == 0 ? 0 : 10, thisLeadingRect.Height ) ),
					nextPointer = nextPointer,
					nextRect = next_rect,
					isNextOnSameLine = false
				};
			}
		}


		static bool IsBefore( TextPointer tp1, TextPointer tp2 )
		{
			return tp1.CompareTo( tp2 ) < 0;
		}

#endif

		void DelayedInvalidateVisual( )
		{
			Dispatcher.BeginInvoke( DispatcherPriority.Background, new Action( InvalidateVisual ) );
		}


		internal void SetRangesToUnderline( IReadOnlyList<(TextPointer start, TextPointer end)> ranges )
		{
			Ranges = ranges;

			DelayedInvalidateVisual( );
		}
	}
}
