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
			var guidelines = new GuidelineSet( );

			guidelines.GuidelinesX.Add( Math.Floor( rect.Left ) );
			guidelines.GuidelinesX.Add( Math.Ceiling( rect.Right ) );
			guidelines.GuidelinesY.Add( Math.Ceiling( rect.Bottom ) );

			guidelines.Freeze( );

			dc.PushGuidelineSet( guidelines );

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

			var bottom_left = rect.BottomLeft;
			var bottom_right = rect.BottomRight;

			dc.DrawLine( Pen, bottom_left, bottom_right );

			if( isLeftStart )
			{
				dc.DrawLine( Pen, bottom_left, bottom_left + new Vector( 0, -3 ) );
			}

			if( isRightEnd )
			{
				dc.DrawLine( Pen, bottom_right, bottom_right + new Vector( 0, -3 ) );
			}

			dc.Pop( );
		}


		struct RectInfo
		{
			public Rect thisRect;
			public TextPointer nextPointer;
			public Rect nextRect;
			public bool isNextOnSameLine;
		}


		RectInfo GetRectInfo( TextPointer thisPointer, Rect thisLeadingRect )
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
