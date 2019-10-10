using RegExpressWPF.Code;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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

		readonly GeometryGroup GeometryGroup = new GeometryGroup( );
		bool MustRecalculateSegments = true;

		public bool IsDbgDisabled; // (disable drawing this adorner for debugging purposes)


		public UnderliningAdorner( UIElement adornedElement ) : base( adornedElement )
		{
			Debug.Assert( adornedElement is RichTextBox );

			Pen.Freeze( );

			IsHitTestVisible = false;

			Rtb.TextChanged += Rtb_TextChanged;
			Rtb.AddHandler( ScrollViewer.ScrollChangedEvent, new RoutedEventHandler( Rtb_ScrollChanged ), true );
		}


		public void SetRangesToUnderline( IReadOnlyList<(TextPointer start, TextPointer end)> ranges )
		{
			if( IsDbgDisabled ) return;

			lock( this )
			{
				if( ranges != null && Ranges != null && ranges.Count == Ranges.Count )
				{
					bool are_different = false;

					for( int i = 0; i < ranges.Count; ++i )
					{
						(TextPointer start, TextPointer end) r = ranges[i];
						(TextPointer start, TextPointer end) R = Ranges[i];

						if( !( r.start.CompareTo( R.start ) == 0 && ( r.end.CompareTo( R.end ) == 0 ) ) )
						{
							are_different = true;
							break;
						}
					}

					if( !are_different )
					{
						return;
					}
				}

				Ranges = ranges;

				MustRecalculateSegments = true;
				DelayedInvalidateVisual( );
			}
		}


		RichTextBox Rtb
		{
			get { return (RichTextBox)AdornedElement; }
		}


		private void Rtb_TextChanged( object sender, TextChangedEventArgs e )
		{
			// (recalculation not needed)

			DelayedInvalidateVisual( );
		}


		private void Rtb_ScrollChanged( object sender, RoutedEventArgs e )
		{
			MustRecalculateSegments = true;
			InvalidateVisual( );
		}


		protected override void OnRender( DrawingContext drawingContext )
		{
			base.OnRender( drawingContext );  // (probably nothing)

			if( IsDbgDisabled ) return;

			lock( this )
			{
				if( MustRecalculateSegments )
				{
					RecalculateSegments( );
				}

				// TODO: use combined geometry?

				var rtb = Rtb;
				var dc = drawingContext;
				var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );

				dc.PushClip( new RectangleGeometry( clip_rect ) );

				dc.DrawGeometry( null, Pen, GeometryGroup );

				dc.Pop( );

				MustRecalculateSegments = false;
			}
		}


		void RecalculateSegments( )
		{
			GeometryGroup.Children.Clear( );

			if( Ranges == null ) return;

			var rtb = Rtb;

			var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );

			var start_doc = rtb.Document.ContentStart;
			var half_pen = Pen.Thickness / 2;

			// TODO: clean 'Ranges' if document was changed (release old document), in thread-safe manner

			foreach( var (start, end) in Ranges )
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

							double last_y = double.NaN;
							double last_x_a = double.NaN;
							double last_x_b = double.NaN;

							for(
								var tp = start.GetNextInsertionPosition( LogicalDirection.Forward );
								tp != null && tp.CompareTo( end ) <= 0;
								tp = tp.GetNextInsertionPosition( LogicalDirection.Forward )
								)
							{
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

								prev_min.Y = Math.Ceiling( prev_min.Y );
								prev_max.Y = Math.Ceiling( prev_max.Y );
								tp_min.Y = Math.Ceiling( tp_min.Y );
								tp_max.Y = Math.Ceiling( tp_max.Y );

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
									var y = a.Y - half_pen;

									if( y < clip_rect.Top - half_pen ) continue; // not visible yet
									if( y > clip_rect.Bottom + half_pen ) break; // already invisible

									bool combined = false;

									if( y == last_y )
									{
										// try to combine with previous one

										if( last_x_a < last_x_b && a.X < b.X && last_x_b == a.X ) // ('==' seems to work)
										{
											last_x_b = b.X;
											combined = true;
										}
										else if( last_x_a > last_x_b && a.X > b.X && last_x_b == b.X )
										{
											last_x_a = a.X;
											combined = true;
										}
									}

									if( !combined )
									{
										if( !double.IsNaN( last_y ) )
										{
											// add accumulated segment
											GeometryGroup.Children.Add( new LineGeometry( new Point( last_x_a, last_y ), new Point( last_x_b, last_y ) ) );
										}

										last_y = y;
										last_x_a = a.X;
										last_x_b = b.X;
									}
								}

								prev_point_b = tp_point_b;
								prev_point_f = tp_point_f;
							}

							// draw accumulated segment
							if( !double.IsNaN( last_y ) )
							{
								// add accumulated segment
								GeometryGroup.Children.Add( new LineGeometry( new Point( last_x_a, last_y ), new Point( last_x_b, last_y ) ) );
							}
						}
					}
				}
			}
		}


		void DelayedInvalidateVisual( )
		{
			Dispatcher.BeginInvoke( DispatcherPriority.Background, new Action( InvalidateVisual ) );
		}

	}
}
