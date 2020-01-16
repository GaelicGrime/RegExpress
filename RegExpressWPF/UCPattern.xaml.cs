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
		readonly StyleInfo PatternCharGroupHighlightStyleInfo;

		int LeftHighlightedParantesis = -1;
		int RightHighlightedParantesis = -1;
		int LeftHighlightedBracket = -1;
		int RightHighlightedBracket = -1;

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
			PatternCharGroupHighlightStyleInfo = new StyleInfo( "PatternCharGroupHighlight" );
			PatternCommentStyleInfo = new StyleInfo( "PatternComment" );

			RecolouringLoop = new ResumableLoop( RecolouringThreadProc, 222, 444 );
			HighlightingLoop = new ResumableLoop( HighlightingThreadProc, 111, 444 );
		}


		public TextData GetTextData( string eol )
		{
			return rtb.GetTextData( eol );
		}


		public SimpleTextData GetSimpleTextData( string eol )
		{
			return rtb.GetSimpleTextData( eol );
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

			var adorner_layer = AdornerLayer.GetAdornerLayer( rtb );
			adorner_layer.Add( WhitespaceAdorner );

			AlreadyLoaded = true;
		}


		private void rtb_TextChanged( object sender, TextChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			UndoRedoHelper.HandleTextChanged( e );

			LeftHighlightedParantesis = -1;
			RightHighlightedParantesis = -1;
			LeftHighlightedBracket = -1;
			RightHighlightedBracket = -1;

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

				if( !td0.Pointers.Any( ) || !td0.Pointers[0].IsInSameDocument( start_doc ) ) return;

				if( cnc.IsCancellationRequested ) return;

				td = td0;
				clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );

				TextPointer top_pointer = rtb.GetPositionFromPoint( new Point( 0, 0 ), snapToText: true ).GetLineStartPosition( -1, out int _ );
				if( cnc.IsCancellationRequested ) return;

				top_index = RtbUtilities.FindNearestBefore( td.Pointers, top_pointer );
				if( cnc.IsCancellationRequested ) return;
				if( top_index < 0 ) top_index = 0;

				TextPointer bottom_pointer = rtb.GetPositionFromPoint( new Point( 0, rtb.ViewportHeight ), snapToText: true ).GetLineStartPosition( +1, out int lines_skipped );
				if( cnc.IsCancellationRequested ) return;

				// (Note. Last pointer from 'td.Pointers' is reserved for end-of-document)
				if( bottom_pointer == null || lines_skipped == 0 )
				{
					bottom_index = td.Pointers.Count - 2;
				}
				else
				{
					bottom_index = RtbUtilities.FindNearestAfter( td.Pointers, bottom_pointer );
					if( cnc.IsCancellationRequested ) return;
				}
				if( bottom_index >= td.Pointers.Count - 1 ) bottom_index = td.Pointers.Count - 2;
				if( bottom_index < top_index ) bottom_index = top_index; // (including 'if bottom_index == 0')
			} );

			if( cnc.IsCancellationRequested ) return;

			if( td == null ) return;
			if( td.Text.Length == 0 ) return;

			Debug.Assert( top_index >= 0 );
			Debug.Assert( bottom_index >= top_index );
			Debug.Assert( bottom_index < td.Pointers.Count );

			//int center_index = ( top_index + bottom_index ) / 2;

			var visible_segment = new Segment( top_index, bottom_index - top_index + 1 );
			var segments_to_colourise = new ColouredSegments( );
			var uncovered_segments = new List<Segment> { new Segment( 0, td.Text.Length ) };

			regex_engine.ColourisePattern( cnc, segments_to_colourise, td.Text, visible_segment );

			if( cnc.IsCancellationRequested ) return;

			RtbUtilities.ApplyStyle( cnc, ChangeEventHelper, null, td, segments_to_colourise.Comments, PatternCommentStyleInfo );
			RtbUtilities.ApplyStyle( cnc, ChangeEventHelper, null, td, segments_to_colourise.Escapes, PatternEscapeStyleInfo );
			RtbUtilities.ApplyStyle( cnc, ChangeEventHelper, null, td, segments_to_colourise.GroupNames, PatternGroupNameStyleInfo );

			foreach( var s in segments_to_colourise.All.SelectMany( s => s ) )
			{
				if( cnc.IsCancellationRequested ) return;

				Segment.Except( uncovered_segments, s );
			}

			var segments_to_uncolour =
				uncovered_segments
					.Select( s => Segment.Intersection( s, visible_segment ) )
					.Where( s => !s.IsEmpty )
					//.OrderBy( s => Math.Abs( center_index - ( s.Index + s.Length / 2 ) ) )
					.ToList( );

			if( cnc.IsCancellationRequested ) return;

			RtbUtilities.ApplyStyle( cnc, ChangeEventHelper, null, td, segments_to_uncolour, PatternNormalStyleInfo );
		}



		void HighlightingThreadProc( ICancellable cnc )
		{
			//..........................
			// TODO: reimplement
			return;
#if false
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

				if( !td0.Pointers.Any( ) || !td0.Pointers[0].IsInSameDocument( start_doc ) ) return;

				if( cnc.IsCancellationRequested ) return;

				td = td0;
				clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );

				TextPointer top_pointer = rtb.GetPositionFromPoint( new Point( 0, 0 ), snapToText: true ).GetLineStartPosition( -1, out int _ );
				if( cnc.IsCancellationRequested ) return;

				top_index = RtbUtilities.FindNearestBefore( td.Pointers, top_pointer );
				if( top_index < 0 ) top_index = 0;

				TextPointer bottom_pointer = rtb.GetPositionFromPoint( new Point( 0, rtb.ViewportHeight ), snapToText: true ).GetLineStartPosition( +1, out int lines_skipped );
				if( cnc.IsCancellationRequested ) return;

				// (Note. Last pointer from 'td.Pointers' is reserved for end-of-document)
				if( bottom_pointer == null || lines_skipped == 0 )
				{
					bottom_index = td.Pointers.Count - 2;
				}
				else
				{
					bottom_index = RtbUtilities.FindNearestAfter( td.Pointers, bottom_pointer );
					if( cnc.IsCancellationRequested ) return;
				}
				if( bottom_index >= td.Pointers.Count - 1 ) bottom_index = td.Pointers.Count - 2;
				if( bottom_index < top_index ) bottom_index = top_index; // (including 'if bottom_index == 0')

				is_focused = rtb.IsFocused;
			} );

			if( cnc.IsCancellationRequested ) return;

			if( td == null ) return;
			if( td.Text.Length == 0 ) return;

			Debug.Assert( top_index >= 0 );
			Debug.Assert( bottom_index >= top_index );
			Debug.Assert( bottom_index < td.Pointers.Count );

			var regex = GetColouringRegex( regex_engine );

			var matches = regex
				.Matches( td.Text )
				.Cast<Match>( )
				.ToArray( );

			if( cnc.IsCancellationRequested ) return;

			int left_para_index = -1;
			int right_para_index = -1;
			int left_bracket_index = -1;
			int right_bracket_index = -1;

			if( is_focused )
			{
				var parentheses = matches.Where( m => m.Groups["para"].Success ).ToArray( );
				if( cnc.IsCancellationRequested ) return;

				var parentheses_at_left = parentheses.Where( m => m.Index < td.SelectionStart ).ToArray( );
				if( cnc.IsCancellationRequested ) return;

				var parentheses_at_right = parentheses.Where( m => m.Index >= td.SelectionStart ).ToArray( );
				if( cnc.IsCancellationRequested ) return;

				if( parentheses_at_left.Any( ) )
				{
					int n = 0;
					int found_i = -1;
					for( int i = parentheses_at_left.Length - 1; i >= 0; --i )
					{
						if( cnc.IsCancellationRequested ) break;

						var m = parentheses_at_left[i];
						if( m.Value == ")" ) --n;
						else if( m.Value == "(" ) ++n;
						if( n == +1 )
						{
							found_i = i;
							break;
						}
					}
					if( found_i >= 0 )
					{
						var m = parentheses_at_left[found_i];

						left_para_index = m.Index;
					}
				}

				if( cnc.IsCancellationRequested ) return;

				if( parentheses_at_right.Any( ) )
				{
					int n = 0;
					int found_i = -1;
					for( int i = 0; i < parentheses_at_right.Length; ++i )
					{
						if( cnc.IsCancellationRequested ) break;

						var m = parentheses_at_right[i];
						if( m.Value == "(" ) --n;
						else if( m.Value == ")" ) ++n;
						if( n == +1 )
						{
							found_i = i;
							break;
						}
					}
					if( found_i >= 0 )
					{
						var m = parentheses_at_right[found_i];

						right_para_index = m.Index;
					}
				}

				if( cnc.IsCancellationRequested ) return;

				var current_group = matches.Where( m => m.Groups["character_group"].Success && m.Index < td.SelectionStart && m.Index + m.Length > td.SelectionStart ).FirstOrDefault( );

				if( cnc.IsCancellationRequested ) return;

				if( current_group != null )
				{
					left_bracket_index = current_group.Index;

					var eog = current_group.Groups["eog"];
					if( eog.Success )
					{
						right_bracket_index = eog.Index;
					}
				}
			}

			if( cnc.IsCancellationRequested ) return;

			lock( Locker )
			{
				ChangeEventHelper.Invoke( CancellationToken.None, ( ) =>
				{
					TryHighlight( ref LeftHighlightedParantesis, td, left_para_index, PatternParaHighlightStyleInfo );
					if( cnc.IsCancellationRequested ) return;
					TryHighlight( ref RightHighlightedParantesis, td, right_para_index, PatternParaHighlightStyleInfo );
					if( cnc.IsCancellationRequested ) return;
					TryHighlight( ref LeftHighlightedBracket, td, left_bracket_index, PatternCharGroupHighlightStyleInfo );
					if( cnc.IsCancellationRequested ) return;
					TryHighlight( ref RightHighlightedBracket, td, right_bracket_index, PatternCharGroupHighlightStyleInfo );
				} );
			}
#endif
		}

#if false
		void TryMark( NaiveRanges ranges, int topIndex, TextData td, int index )
		{
			if( index >= 0 )
			{
				ranges.SafeSet( index - topIndex );
			}
		}


		void TryHighlight( ref int savedIndex, TextData td, int index, StyleInfo styleInfo )
		{
			// TODO: avoid flickering

			if( savedIndex >= 0 && savedIndex != index )
			{
				var tr = td.Range( savedIndex, 1 );
				tr.Style( PatternNormalStyleInfo );
			}

			savedIndex = index;

			if( savedIndex >= 0 )
			{
				var tr = td.RangeFB( savedIndex, 1 );
				tr.Style( styleInfo );
			}
		}
#endif

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
