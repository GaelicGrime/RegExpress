using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using RegexEngineInfrastructure;
using RegexEngineInfrastructure.SyntaxColouring;
using RegExpressWPF.Adorners;
using RegExpressWPF.Code;


namespace RegExpressWPF
{
	/// <summary>
	/// Interaction logic for UCPattern.xaml
	/// </summary>
	public partial class UCPattern : UserControl, IDisposable
	{
		readonly WhitespaceAdorner WhitespaceAdorner;

		readonly ResumableLoop RecolouringLoop;
		readonly ResumableLoop HighlightingLoop;

		readonly ChangeEventHelper ChangeEventHelper;
		readonly UndoRedoHelper UndoRedoHelper;

		bool AlreadyLoaded = false;

		readonly StyleInfo PatternNormalStyleInfo;
		readonly StyleInfo PatternGroupNameStyleInfo;
		readonly StyleInfo PatternEscapeStyleInfo;
		readonly StyleInfo PatternCommentStyleInfo;

		readonly StyleInfo PatternParaHighlightStyleInfo;
		readonly StyleInfo PatternCharGroupBracketHighlightStyleInfo;
		readonly StyleInfo PatternRangeCurlyBraceHighlightStyleInfo;

		Segment LeftHighlightedParantesis = Segment.Empty;
		Segment RightHighlightedParantesis = Segment.Empty;
		Segment LeftHighlightedBracket = Segment.Empty;
		Segment RightHighlightedBracket = Segment.Empty;
		Segment LeftHighlightedCurlyBrace = Segment.Empty;
		Segment RightHighlightedCurlyBrace = Segment.Empty;

		IRegexEngine mRegexEngine;
		string mEol;

		public event EventHandler TextChanged;


		public UCPattern( )
		{
			InitializeComponent( );

			ChangeEventHelper = new ChangeEventHelper( this.rtb );
			UndoRedoHelper = new UndoRedoHelper( this.rtb );

			WhitespaceAdorner = new WhitespaceAdorner( rtb, ChangeEventHelper );

			PatternNormalStyleInfo = new StyleInfo( "PatternNormal" );
			PatternParaHighlightStyleInfo = new StyleInfo( "PatternParaHighlight" );
			PatternGroupNameStyleInfo = new StyleInfo( "PatternGroupName" );
			PatternEscapeStyleInfo = new StyleInfo( "PatternEscape" );
			PatternCharGroupBracketHighlightStyleInfo = new StyleInfo( "PatternCharGroupHighlight" );
			PatternRangeCurlyBraceHighlightStyleInfo = PatternCharGroupBracketHighlightStyleInfo;
			PatternCommentStyleInfo = new StyleInfo( "PatternComment" );

			RecolouringLoop = new ResumableLoop( RecolouringThreadProc, 222, 444 );
			HighlightingLoop = new ResumableLoop( HighlightingThreadProc, 111, 444 );

			//WhitespaceAdorner.IsDbgDisabled = true;
		}


		public BaseTextData GetBaseTextData( string eol )
		{
			return rtb.GetBaseTextData( eol );
		}


		public TextData GetTextData( string eol )
		{
			return rtb.GetTextData( eol );
		}


		public void SetText( string value )
		{
			RtbUtilities.SetText( rtb, value );

			UndoRedoHelper.Init( );
		}


		public void SetRegexOptions( IRegexEngine engine, string eol )
		{
			StopAll( );

			lock( this )
			{
				mRegexEngine = engine;
				mEol = eol;
			}

			if( IsLoaded )
			{
				RecolouringLoop.SendRestart( );
				HighlightingLoop.SendRestart( );
			}
		}


		public void ShowWhiteSpaces( bool yes )
		{
			WhitespaceAdorner.ShowWhiteSpaces( yes );
		}


		public void SetFocus( )
		{
			rtb.Focus( );
			rtb.Selection.Select( rtb.Document.ContentStart, rtb.Document.ContentStart );
		}


		public void StopAll( )
		{
			// TODO: stop threads

			RecolouringLoop.SendStop( );
			HighlightingLoop.SendStop( );
		}


		private void UserControl_Loaded( object sender, RoutedEventArgs e )
		{
			if( AlreadyLoaded ) return;

			// TODO: add an option
			//rtb.Document.MinPageWidth = Utilities.ToPoints( "21cm" );

			var adorner_layer = AdornerLayer.GetAdornerLayer( rtb );
			adorner_layer.Add( WhitespaceAdorner );

			AlreadyLoaded = true;
		}


		private void rtb_TextChanged( object sender, TextChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			UndoRedoHelper.HandleTextChanged( e );

			LeftHighlightedParantesis = Segment.Empty;
			RightHighlightedParantesis = Segment.Empty;
			LeftHighlightedBracket = Segment.Empty;
			RightHighlightedBracket = Segment.Empty;
			LeftHighlightedCurlyBrace = Segment.Empty;
			RightHighlightedCurlyBrace = Segment.Empty;

			RecolouringLoop.SendRestart( );
			HighlightingLoop.SendRestart( );

			TextChanged?.Invoke( this, null );
		}


		private void rtb_SelectionChanged( object sender, RoutedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;
			if( !rtb.IsFocused ) return;

			UndoRedoHelper.HandleSelectionChanged( );
			HighlightingLoop.SendRestart( );
		}


		private void rtb_ScrollChanged( object sender, ScrollChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			RecolouringLoop.SendRestart( );
			HighlightingLoop.SendRestart( );
		}


		private void rtb_SizeChanged( object sender, SizeChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			RecolouringLoop.SendRestart( );
			HighlightingLoop.SendRestart( );
		}


		private void rtb_GotFocus( object sender, RoutedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			HighlightingLoop.SendRestart( );

			if( Properties.Settings.Default.BringCaretIntoView )
			{
				var p = rtb.CaretPosition?.Parent as FrameworkContentElement;
				if( p != null )
				{
					p.BringIntoView( );
				}
			}
		}


		private void rtb_LostFocus( object sender, RoutedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			HighlightingLoop.SendRestart( );
		}


		private void rtb_Pasting( object sender, DataObjectPastingEventArgs e )
		{
			if( e.DataObject.GetDataPresent( DataFormats.UnicodeText ) )
			{
				e.FormatToApply = DataFormats.UnicodeText;
			}
			else if( e.DataObject.GetDataPresent( DataFormats.Text ) )
			{
				e.FormatToApply = DataFormats.Text;
			}
			else
			{
				e.CancelCommand( );
			}
		}


		readonly object Locker = new object( );


		void RecolouringThreadProc( ICancellable cnc )
		{
			IRegexEngine regex_engine;
			string eol;

			lock( this )
			{
				regex_engine = mRegexEngine;
				eol = mEol;
			}

			if( regex_engine == null ) return;

			TextData td = null;
			Rect clip_rect = Rect.Empty;
			int top_index = 0;
			int bottom_index = 0;

			UITaskHelper.Invoke( rtb, ( ) =>
			{
				td = null;

				var start_doc = rtb.Document.ContentStart;
				var end_doc = rtb.Document.ContentStart;

				if( !start_doc.HasValidLayout || !end_doc.HasValidLayout ) return;

				var td0 = rtb.GetTextData( eol );

				if( cnc.IsCancellationRequested ) return;

				td = td0;
				clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );

				TextPointer top_pointer = rtb.GetPositionFromPoint( new Point( 0, 0 ), snapToText: true ).GetLineStartPosition( -1, out int _ );
				if( cnc.IsCancellationRequested ) return;

				top_index = td.TextPointers.GetIndex( top_pointer, LogicalDirection.Backward );
				if( cnc.IsCancellationRequested ) return;
				if( top_index < 0 ) top_index = 0;

				TextPointer bottom_pointer = rtb.GetPositionFromPoint( new Point( 0, rtb.ViewportHeight ), snapToText: true ).GetLineStartPosition( +1, out int lines_skipped );
				if( cnc.IsCancellationRequested ) return;

				if( bottom_pointer == null || lines_skipped == 0 )
				{
					bottom_index = td.Text.Length;
				}
				else
				{
					bottom_index = td.TextPointers.GetIndex( bottom_pointer, LogicalDirection.Forward );
					if( cnc.IsCancellationRequested ) return;
				}
				if( bottom_index > td.Text.Length ) bottom_index = td.Text.Length;
				if( bottom_index < top_index ) bottom_index = top_index; // (including 'if bottom_index == 0')
			} );

			if( cnc.IsCancellationRequested ) return;

			if( td == null ) return;
			if( td.Text.Length == 0 ) return;

			Debug.Assert( top_index >= 0 );
			Debug.Assert( bottom_index >= top_index );
			//Debug.Assert( bottom_index < td.OldPointers.Count );
			Debug.Assert( bottom_index <= td.Text.Length );

			var visible_segment = new Segment( top_index, bottom_index - top_index + 1 );
			var segments_to_colourise = new ColouredSegments( );

			regex_engine.ColourisePattern( cnc, segments_to_colourise, td.Text, visible_segment );

			if( cnc.IsCancellationRequested ) return;

			int center_index = ( top_index + bottom_index ) / 2;

			var arranged_escapes = segments_to_colourise.Escapes
				.OrderBy( s => Math.Abs( center_index - ( s.Index + s.Length / 2 ) ) )
				.ToList( );

			RtbUtilities.ApplyStyle( cnc, ChangeEventHelper, null, td, segments_to_colourise.Comments, PatternCommentStyleInfo );
			RtbUtilities.ApplyStyle( cnc, ChangeEventHelper, null, td, arranged_escapes, PatternEscapeStyleInfo );
			RtbUtilities.ApplyStyle( cnc, ChangeEventHelper, null, td, segments_to_colourise.GroupNames, PatternGroupNameStyleInfo );

			var uncovered_segments = new List<Segment> { new Segment( 0, td.Text.Length ) };

			foreach( var s in segments_to_colourise.All.SelectMany( s => s ) )
			{
				if( cnc.IsCancellationRequested ) return;

				Segment.Except( uncovered_segments, s );
			}

			Segment.Except( uncovered_segments, LeftHighlightedParantesis );
			Segment.Except( uncovered_segments, RightHighlightedParantesis );
			Segment.Except( uncovered_segments, LeftHighlightedBracket );
			Segment.Except( uncovered_segments, RightHighlightedBracket );
			Segment.Except( uncovered_segments, LeftHighlightedCurlyBrace );
			Segment.Except( uncovered_segments, RightHighlightedCurlyBrace );

			var segments_to_uncolour =
				uncovered_segments
					.Select( s => Segment.Intersection( s, visible_segment ) )
					.Where( s => !s.IsEmpty )
					.OrderBy( s => Math.Abs( center_index - ( s.Index + s.Length / 2 ) ) )
					.ToList( );

			if( cnc.IsCancellationRequested ) return;

			RtbUtilities.ApplyStyle( cnc, ChangeEventHelper, null, td, segments_to_uncolour, PatternNormalStyleInfo );
		}



		void HighlightingThreadProc( ICancellable cnc )
		{
			IRegexEngine regex_engine;
			string eol;

			lock( this )
			{
				regex_engine = mRegexEngine;
				eol = mEol;
			}

			if( regex_engine == null ) return;

			TextData td = null;
			Rect clip_rect = Rect.Empty;
			int top_index = 0;
			int bottom_index = 0;
			bool is_focused = false;

			UITaskHelper.Invoke( rtb, ( ) =>
			{
				td = null;

				var start_doc = rtb.Document.ContentStart;
				var end_doc = rtb.Document.ContentStart;

				if( !start_doc.HasValidLayout || !end_doc.HasValidLayout ) return;

				var td0 = rtb.GetTextData( eol );

				if( cnc.IsCancellationRequested ) return;

				td = td0;
				clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );

				TextPointer top_pointer = rtb.GetPositionFromPoint( new Point( 0, 0 ), snapToText: true ).GetLineStartPosition( -1, out int _ );
				if( cnc.IsCancellationRequested ) return;

				top_index = td.TextPointers.GetIndex( top_pointer, LogicalDirection.Backward );
				if( top_index < 0 ) top_index = 0;

				TextPointer bottom_pointer = rtb.GetPositionFromPoint( new Point( 0, rtb.ViewportHeight ), snapToText: true ).GetLineStartPosition( +1, out int lines_skipped );
				if( cnc.IsCancellationRequested ) return;

				if( bottom_pointer == null || lines_skipped == 0 )
				{
					bottom_index = td.Text.Length;
				}
				else
				{
					bottom_index = td.TextPointers.GetIndex( bottom_pointer, LogicalDirection.Forward );
					if( cnc.IsCancellationRequested ) return;
				}
				if( bottom_index > td.Text.Length ) bottom_index = td.Text.Length;
				if( bottom_index < top_index ) bottom_index = top_index; // (including 'if bottom_index == 0')

				is_focused = rtb.IsFocused;
			} );

			if( cnc.IsCancellationRequested ) return;

			if( td == null ) return;
			if( td.Text.Length == 0 ) return;

			Debug.Assert( top_index >= 0 );
			Debug.Assert( bottom_index >= top_index );
			Debug.Assert( bottom_index <= td.Text.Length );

			Highlights highlights = null;

			if( is_focused )
			{
				var visible_segment = new Segment( top_index, bottom_index - top_index + 1 );

				highlights = new Highlights( );

				regex_engine.HighlightPattern( cnc, highlights, td.Text, td.SelectionStart, td.SelectionEnd, visible_segment );
			}

			if( cnc.IsCancellationRequested ) return;

			lock( Locker )
			{
				ChangeEventHelper.Invoke( CancellationToken.None, ( ) =>
				{
					TryHighlight( ref LeftHighlightedParantesis, highlights?.LeftPar ?? Segment.Empty, td, PatternParaHighlightStyleInfo );
					if( cnc.IsCancellationRequested ) return;

					TryHighlight( ref RightHighlightedParantesis, highlights?.RightPar ?? Segment.Empty, td, PatternParaHighlightStyleInfo );
					if( cnc.IsCancellationRequested ) return;

					TryHighlight( ref LeftHighlightedBracket, highlights?.LeftBracket ?? Segment.Empty, td, PatternCharGroupBracketHighlightStyleInfo );
					if( cnc.IsCancellationRequested ) return;

					TryHighlight( ref RightHighlightedBracket, highlights?.RightBracket ?? Segment.Empty, td, PatternCharGroupBracketHighlightStyleInfo );
					if( cnc.IsCancellationRequested ) return;

					TryHighlight( ref LeftHighlightedCurlyBrace, highlights?.LeftCurlyBrace ?? Segment.Empty, td, PatternRangeCurlyBraceHighlightStyleInfo );
					if( cnc.IsCancellationRequested ) return;

					TryHighlight( ref RightHighlightedCurlyBrace, highlights?.RightCurlyBrace ?? Segment.Empty, td, PatternRangeCurlyBraceHighlightStyleInfo );
					if( cnc.IsCancellationRequested ) return;
				} );
			}
		}


		void TryHighlight( ref Segment currentSegment, Segment newSegment, TextData td, StyleInfo styleInfo )
		{
			// TODO: avoid flickering

			if( !currentSegment.IsEmpty && currentSegment != newSegment )
			{
				var tr = td.Range( currentSegment );
				tr.Style( PatternNormalStyleInfo );
			}

			currentSegment = newSegment;

			if( !currentSegment.IsEmpty )
			{
				var tr = td.RangeFB( currentSegment.Index, currentSegment.Length );
				tr.Style( styleInfo );
			}
		}


		#region IDisposable Support

		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose( bool disposing )
		{
			if( !disposedValue )
			{
				if( disposing )
				{
					// TODO: dispose managed state (managed objects).

					using( RecolouringLoop ) { }
					using( HighlightingLoop ) { }
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~UCPattern()
		// {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose( )
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose( true );
			// TODO: uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}

		#endregion

	}
}
