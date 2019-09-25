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
		readonly Pen WsPen = new Pen( Brushes.Transparent, 0 );
		readonly Brush WsBrush = Brushes.LightSeaGreen;
		readonly Pen TabPen = new Pen( Brushes.LightSeaGreen, 1 );
		readonly Pen EofPen = new Pen( Brushes.LightSeaGreen, 1 );
		readonly Brush EofBrush = Brushes.Transparent;


		public WhitespaceAdorner( UIElement adornedElement ) : base( adornedElement )
		{
			Debug.Assert( adornedElement is MyRichTextBox );

			IsHitTestVisible = false;

			Rtb.TextChanged += Rtb_TextChanged;
			Rtb.AddHandler( ScrollViewer.ScrollChangedEvent, new RoutedEventHandler( Rtb_ScrollChanged ), true );
		}


		MyRichTextBox Rtb
		{
			get { return (MyRichTextBox)AdornedElement; }
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
			var td = rtb.GetTextData( null );

			ShowSpaces( dc, td );
			ShowTabs( dc, td );
			//ShowEof( dc, td );
		}


		protected override void OnRenderSizeChanged( SizeChangedInfo sizeInfo )
		{
			base.OnRenderSizeChanged( sizeInfo );

			Invalidate( );
		}


		void Invalidate( )
		{
			Dispatcher.BeginInvoke( DispatcherPriority.Background, new Action( InvalidateVisual ) );
		}


		void ShowSpaces( DrawingContext dc, TextData td )
		{
			/*
			var rtb = Rtb;

			var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );
			dc.PushClip( new RectangleGeometry( clip_rect ) );

			foreach( Match m in Regex.Matches( td.Text, @"(\p{Zs}+)(?:\n|\r|$)" ) )
			{
				var g = m.Groups[1];
				var start = td.Pointers[g.Index];
				var end = td.Pointers[g.Index + g.Length];
				if( start.HasValidLayout && end.HasValidLayout )
				{
					var start_rect = start.GetCharacterRect( LogicalDirection.Forward );
					var end_rect = end.GetCharacterRect( LogicalDirection.Backward );

					// TODO: consider wrapped text

					var w = end_rect.Right - start_rect.Left;

					if( w > 0 )
					{
						var r = new Rect( start_rect.Left, start_rect.Top, w, start_rect.Height );

						if( !r.IsEmpty && r.IntersectsWith( clip_rect ) )
						{
							var y = r.Top + r.Height / 2;

							for( var i = 0; i < g.Length; ++i )
							{
								var x = r.Left + r.Width * i / g.Length + r.Width / g.Length / 2;

								var p = new Rect( x, y, 2, 2 );

								dc.DrawRectangle( WsBrush, WsPen, p );
							}
						}
					}
				}
			}

			dc.Pop( );
			*/

			const string pattern = @"(\p{Zs})\1+";
			var re = new Regex( pattern );

			var rtb = Rtb;
			var start_doc = Rtb.Document.ContentStart;

			var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );
			dc.PushClip( new RectangleGeometry( clip_rect ) );

			var guidelines = new GuidelineSet( );


			foreach( Match match in re.Matches( td.Text ) )
			{
				var left = td.Pointers[match.Index];
				var right = td.Pointers[match.Index + match.Length];

				if( left.HasValidLayout && right.HasValidLayout &&
					left.IsInSameDocument( start_doc ) && right.IsInSameDocument( start_doc ) )
				{
					left = left.GetInsertionPosition( LogicalDirection.Forward );
					right = right.GetInsertionPosition( LogicalDirection.Backward );

					var rect_left = left.GetCharacterRect( LogicalDirection.Forward );
					var rect_right = right.GetCharacterRect( LogicalDirection.Backward );

					var rect = new Rect( rect_left.TopLeft, rect_right.BottomRight );

					if( !rect.IsEmpty && rect.IntersectsWith( clip_rect ) )
					{
						const int DOT_SIZE = 2;
						var y = Math.Ceiling( rect.Top + rect.Height / 2 - DOT_SIZE / 2);
						var w = rect.Width / match.Length;

						guidelines.GuidelinesY.Clear( );
						guidelines.GuidelinesY.Add( y );

						dc.PushGuidelineSet( guidelines );


						for( var i = 0; i < match.Length; ++i )
						{
							var x = rect.Left + rect.Width * i / match.Length + w / 2;
							var p = new Rect( x, y, DOT_SIZE, DOT_SIZE );

							dc.DrawRectangle( WsBrush, WsPen, p );
						}

						dc.Pop( );
					}
				}
			}

			dc.Pop( );
		}


		void ShowTabs( DrawingContext dc, TextData td )
		{
			var rtb = Rtb;
			var start_doc = Rtb.Document.ContentStart;

			var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );
			dc.PushClip( new RectangleGeometry( clip_rect ) );

			var guidelines = new GuidelineSet( );

			foreach( Match match in Regex.Matches( td.Text, @"\t" ) )
			{
				var left = td.Pointers[match.Index];

				if( left.HasValidLayout && left.IsInSameDocument( start_doc ) )
				{
					left = left.GetInsertionPosition( LogicalDirection.Forward );

					var rect = left.GetCharacterRect( LogicalDirection.Forward );
					const int ARROW_WIDTH = 8;
					rect.Width = ARROW_WIDTH;
					rect.Offset( 2, 0 );

					if( rect.IntersectsWith( clip_rect ) )
					{
						var x = rect.Left;
						var y = Math.Ceiling( rect.Top + rect.Height / 2 );

						guidelines.GuidelinesY.Clear( );
						guidelines.GuidelinesY.Add( y );

						dc.PushGuidelineSet( guidelines );

						dc.DrawLine( TabPen, new Point( x, y ), new Point( x + ARROW_WIDTH, y ) );
						dc.DrawLine( TabPen, new Point( x + ARROW_WIDTH / 2, y - ARROW_WIDTH / 2 ), new Point( x + ARROW_WIDTH, y ) );
						dc.DrawLine( TabPen, new Point( x + ARROW_WIDTH / 2, y + ARROW_WIDTH / 2 ), new Point( x + ARROW_WIDTH, y ) );

						dc.Pop( );
					}
				}
			}

			dc.Pop( );
		}


		void ShowEof( DrawingContext dc, TextData td )
		{
			if( Regex.IsMatch( td.Text, @"(\r|\n)$", RegexOptions.ExplicitCapture ) )
			{
				var rtb = Rtb;

				if( rtb.Document.ContentEnd.HasValidLayout )
				{
					var rect = rtb.Document.ContentEnd.GetCharacterRect( LogicalDirection.Forward ); // (no width)

					if( !rect.IsEmpty )
					{
						double w = 5;
						double h = rect.Height * 0.4;
						double top = rect.Top + ( rect.Height - h ) / 2.0;

						var eof_rect = new Rect( new Point( rect.Left + 3, top ), new Size( w, h ) );
						var clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );

						dc.PushClip( new RectangleGeometry( clip_rect ) );

						dc.DrawRectangle( EofBrush, EofPen, eof_rect );

						dc.Pop( );
					}
				}
			}
		}
	}
}

