using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Media;
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
using RegExpressWPF.Adorners;
using RegExpressWPF.Code;


namespace RegExpressWPF
{
	/// <summary>
	/// Interaction logic for UCText.xaml
	/// </summary>
	public partial class UCText : UserControl, IDisposable
	{
		readonly WhitespaceAdorner WhitespaceAdorner;
		readonly UnderliningAdorner LocalUnderliningAdorner;
		readonly UnderliningAdorner ExternalUnderliningAdorner;

		readonly ResumableLoop RecolouringLoop;
		readonly ResumableLoop LocalUnderliningLoop;
		readonly ResumableLoop ExternalUnderliningLoop;

		readonly ChangeEventHelper ChangeEventHelper;
		readonly UndoRedoHelper UndoRedoHelper;

		bool AlreadyLoaded = false;

		IReadOnlyList<Match> LastMatches;
		bool LastShowCaptures;
		string LastEol;
		IReadOnlyList<Segment> LastExternalUnderliningSegments;
		bool LastExternalUnderliningSetSelection;

		readonly StyleInfo NormalStyleInfo;
		readonly StyleInfo[] HighlightStyleInfos;

		readonly LengthConverter LengthConverter = new LengthConverter( );


		public event EventHandler TextChanged;
		public event EventHandler SelectionChanged;
		public event EventHandler LocalUnderliningFinished;


		public UCText( )
		{
			InitializeComponent( );

			ChangeEventHelper = new ChangeEventHelper( this.rtb );
			UndoRedoHelper = new UndoRedoHelper( this.rtb );

			WhitespaceAdorner = new WhitespaceAdorner( rtb, ChangeEventHelper );
			LocalUnderliningAdorner = new UnderliningAdorner( rtb );
			ExternalUnderliningAdorner = new UnderliningAdorner( rtb );

			NormalStyleInfo = new StyleInfo( "TextNormal" );

			HighlightStyleInfos = new[]
			{
				new StyleInfo( "MatchHighlight_0" ),
				new StyleInfo( "MatchHighlight_1" ),
				new StyleInfo( "MatchHighlight_2" )
			};


			RecolouringLoop = new ResumableLoop( RecolouringThreadProc, 333, 555 );
			LocalUnderliningLoop = new ResumableLoop( LocalUnderliningThreadProc, 222, 444 );
			ExternalUnderliningLoop = new ResumableLoop( ExternalUnderliningThreadProc, 333, 555 );


			pnlDebug.Visibility = Visibility.Collapsed;
#if !DEBUG
			pnlDebug.Visibility = Visibility.Collapsed;
#endif
			//WhitespaceAdorner.IsDbgDisabled = true;
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


		public void SetMatches( IReadOnlyList<Match> matches, bool showCaptures, string eol )
		{
			if( matches == null ) throw new ArgumentNullException( nameof( matches ) );

			IReadOnlyList<Match> last_matches;
			bool last_show_captures;
			string last_eol;

			lock( this )
			{
				last_matches = LastMatches;
				last_show_captures = LastShowCaptures;
				last_eol = LastEol;
			}

			if( last_matches != null )
			{
				var old_groups = last_matches.SelectMany( m => m.Groups.Cast<Group>( ) ).Select( g => (g.Index, g.Length, g.Value) );
				var new_groups = matches.SelectMany( m => m.Groups.Cast<Group>( ) ).Select( g => (g.Index, g.Length, g.Value) );

				if( new_groups.SequenceEqual( old_groups ) &&
					showCaptures == last_show_captures &&
					eol == last_eol )
				{
					lock( this )
					{
						LastMatches = matches;
						LastExternalUnderliningSegments = null;
					}
					return;
				}
			}

			RecolouringLoop.SendStop( );
			LocalUnderliningLoop.SendStop( );
			ExternalUnderliningLoop.SendStop( );

			lock( this )
			{
				LastMatches = matches;
				LastShowCaptures = showCaptures;
				LastEol = eol;
				LastExternalUnderliningSegments = null;
			}

			RecolouringLoop.SendRestart( );
			LocalUnderliningLoop.SendRestart( );
			ExternalUnderliningLoop.SendRestart( );
		}


		public void ShowWhiteSpaces( bool yes )
		{
			WhitespaceAdorner.ShowWhiteSpaces( yes );
		}


		public IReadOnlyList<Segment> GetUnderliningInfo( )
		{
			if( LastMatches == null )
			{
				return Enumerable.Empty<Segment>( ).ToList( );
			}

			TextData td = null;

			if( !CheckAccess( ) )
			{
				ChangeEventHelper.Invoke( CancellationToken.None, ( ) =>
				{
					td = rtb.GetTextData( LastEol );
				} );
			}
			else
			{
				td = rtb.GetTextData( LastEol );
			}

			return GetUnderliningInfo( NonCancellable.Instance, td, LastMatches, LastShowCaptures );
		}


		public void SetExternalUnderlining( IReadOnlyList<Segment> segments, bool setSelection )
		{
			ExternalUnderliningLoop.SendStop( );

			lock( this )
			{
				LastExternalUnderliningSegments = segments;
				LastExternalUnderliningSetSelection = setSelection;
			}

			ExternalUnderliningLoop.SendRestart( );
		}


		public void StopAll( )
		{
			RecolouringLoop.SendStop( );
			LocalUnderliningLoop.SendStop( );
			ExternalUnderliningLoop.SendStop( );
		}


		public void NextMatch( LogicalDirection direction )
		{
			IReadOnlyList<Match> matches;
			string eol;

			lock( this )
			{
				matches = LastMatches;
				eol = LastEol;
			}

			if( matches == null || !matches.Any( ) )
			{
				SystemSounds.Asterisk.Play( );
				return;
			}

			var td = rtb.GetTextData( eol );

			// find active match
			int active_i = -1;

			for( int i = 0; i < matches.Count; i++ )
			{
				var m = matches[i];

				if( td.SelectionStart >= m.Index && td.SelectionStart < m.Index + m.Length )
				{
					active_i = i;
					break;
				}
			}

			if( active_i >= 0 )
			{
				var next_i = direction == LogicalDirection.Backward ? active_i - 1 : active_i + 1;
				if( next_i >= matches.Count ) next_i = 0;
				if( next_i < 0 ) next_i = matches.Count - 1;

				var next_match = matches[next_i];

				RtbUtilities.SafeSelect( rtb, td, next_match.Index, next_match.Index + next_match.Length );
			}
			else
			{
				//...
				// TODO: implement.
			}
		}


		private void UserControl_Loaded( object sender, RoutedEventArgs e )
		{
			if( AlreadyLoaded ) return;

			rtb.Document.MinPageWidth = (double)LengthConverter.ConvertFromString( "21cm" );

			var adorner_layer = AdornerLayer.GetAdornerLayer( rtb );
			adorner_layer.Add( WhitespaceAdorner );
			adorner_layer.Add( LocalUnderliningAdorner );
			adorner_layer.Add( ExternalUnderliningAdorner );

			AlreadyLoaded = true;
		}


		private void rtb_SelectionChanged( object sender, RoutedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;
			if( !rtb.IsFocused ) return;

			LocalUnderliningLoop.SendRestart( );

			UndoRedoHelper.HandleSelectionChanged( );

			SelectionChanged?.Invoke( this, null );

			ShowDebugInformation( ); // #if DEBUG
		}


		private void rtb_TextChanged( object sender, TextChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			RecolouringLoop.SendStop( );
			LocalUnderliningLoop.SendStop( );
			ExternalUnderliningLoop.SendStop( );

			UndoRedoHelper.HandleTextChanged( e );

			//...
			//lock( this )
			//{
			//	LastMatches = null;
			//	LastShowCaptures = false;
			//	LastEol = null;
			//}

			TextChanged?.Invoke( this, null );
		}


		private void rtb_ScrollChanged( object sender, ScrollChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			RecolouringLoop.SendRestart( );
		}


		private void rtb_SizeChanged( object sender, SizeChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			RecolouringLoop.SendRestart( );
		}


		private void rtb_GotFocus( object sender, RoutedEventArgs e )
		{
			LocalUnderliningLoop.SendRestart( );
			ExternalUnderliningLoop.SendRestart( );

			if( Properties.Settings.Default.BringCaretIntoView )
			{
				var p = rtb.CaretPosition.Parent as FrameworkContentElement;
				if( p != null )
				{
					p.BringIntoView( );
				}
			}
		}


		private void rtb_LostFocus( object sender, RoutedEventArgs e )
		{
			LocalUnderliningLoop.SendRestart( );
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


		private void btnDbgSave_Click( object sender, RoutedEventArgs e )
		{
#if DEBUG
			rtb.Focus( );

			Utilities.DbgSaveXAML( @"debug-uctext.xml", rtb.Document );

			SaveToPng( Window.GetWindow( this ), "debug-uctext.png" );
#endif
		}


		private void btnDbgInsertB_Click( object sender, RoutedEventArgs e )
		{
#if DEBUG
			var p = rtb.Selection.Start.GetInsertionPosition( LogicalDirection.Backward );
			if( p == null )
			{
				SystemSounds.Beep.Play( );
			}
			else
			{
				rtb.Selection.Select( p, p );
				rtb.Focus( );
			}
#endif
		}

		private void btnDbgInsertF_Click( object sender, RoutedEventArgs e )
		{
#if DEBUG
			var p = rtb.Selection.Start.GetInsertionPosition( LogicalDirection.Forward );
			if( p == null )
			{
				SystemSounds.Beep.Play( );
			}
			else
			{
				rtb.Selection.Select( p, p );
				rtb.Focus( );
			}
#endif
		}


		private void btnDbgNextInsert_Click( object sender, RoutedEventArgs e )
		{
#if DEBUG
			var p = rtb.Selection.Start.GetNextInsertionPosition( LogicalDirection.Forward );
			if( p == null )
			{
				SystemSounds.Beep.Play( );
			}
			else
			{
				rtb.Selection.Select( p, p );
				rtb.Focus( );
			}
#endif
		}

		private void btnDbgNextContext_Click( object sender, RoutedEventArgs e )
		{
#if DEBUG
			var p = rtb.Selection.Start.GetNextContextPosition( LogicalDirection.Forward );
			if( p == null )
			{
				SystemSounds.Beep.Play( );
			}
			else
			{
				rtb.Selection.Select( p, p );
				rtb.Focus( );
			}
#endif
		}

#if DEBUG
		// https://blogs.msdn.microsoft.com/kirillosenkov/2009/10/12/saving-images-bmp-png-etc-in-wpfsilverlight/
		void SaveToPng( FrameworkElement visual, string fileName )
		{
			var encoder = new PngBitmapEncoder( );
			SaveUsingEncoder( visual, fileName, encoder );
		}

		void SaveUsingEncoder( FrameworkElement visual, string fileName, BitmapEncoder encoder )
		{
			RenderTargetBitmap bitmap = new RenderTargetBitmap(
				(int)visual.ActualWidth,
				(int)visual.ActualHeight,
				96,
				96,
				PixelFormats.Pbgra32 );
			bitmap.Render( visual );
			BitmapFrame frame = BitmapFrame.Create( bitmap );
			encoder.Frames.Add( frame );

			using( var stream = File.Create( fileName ) )
			{
				encoder.Save( stream );
			}
		}
#endif


		void RecolouringThreadProc( ICancellable cnc )
		{
			IReadOnlyList<Match> matches;
			string eol;
			bool show_captures;

			lock( this )
			{
				matches = LastMatches;
				eol = LastEol;
				show_captures = LastShowCaptures;
			}

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

			// (NOTE. Overlaps are possible in this example: (?=(..))

			var coloured_ranges = new NaiveRanges( bottom_index - top_index + 1 );
			var segments_and_styles = new List<(Segment segment, StyleInfo styleInfo)>( );

			if( matches != null )
			{
				for( int i = 0; i < matches.Count; ++i )
				{
					if( cnc.IsCancellationRequested ) break;

					Match match = matches[i];
					Debug.Assert( match.Success );

					// TODO: consider these conditions for bi-directional text
					if( match.Index + match.Length < top_index ) continue;
					if( match.Index > bottom_index ) continue; // (do not break; the order of indices is unspecified)

					var highlight_index = unchecked(i % HighlightStyleInfos.Length);

					coloured_ranges.SafeSet( match.Index - top_index, match.Length );
					segments_and_styles.Add( (new Segment( match.Index, match.Length ), HighlightStyleInfos[highlight_index]) );
				}
			}

			if( cnc.IsCancellationRequested ) return;

			List<(Segment segment, StyleInfo styleInfo)> segments_to_uncolour =
							coloured_ranges
								.GetSegments( cnc, false, top_index )
								.Select( s => (s, NormalStyleInfo) )
								.ToList( );

			if( cnc.IsCancellationRequested ) return;

			int center_index = ( top_index + bottom_index ) / 2;

			var all_segments_and_styles =
				segments_and_styles.Concat( segments_to_uncolour )
				.OrderBy( s => Math.Abs( center_index - ( s.segment.Index + s.segment.Length / 2 ) ) )
				.ToList( );

			if( cnc.IsCancellationRequested ) return;

			RtbUtilities.ApplyStyle( cnc, ChangeEventHelper, pbProgress, td, all_segments_and_styles );

			if( cnc.IsCancellationRequested ) return;

			UITaskHelper.BeginInvoke( pbProgress, ( ) =>
						{
							pbProgress.Visibility = Visibility.Hidden;
						} );
		}


		void LocalUnderliningThreadProc( ICancellable cnc )
		{
			bool is_focussed = false;
			IReadOnlyList<Match> matches;
			string eol;
			bool show_captures;

			lock( this )
			{
				matches = LastMatches;
				eol = LastEol;
				show_captures = LastShowCaptures;
			}

			if( matches == null ) return;

			TextData td = null;

			ChangeEventHelper.Invoke( CancellationToken.None, ( ) =>
			{
				is_focussed = rtb.IsFocused;
				if( is_focussed ) td = rtb.GetTextData( eol );
			} );

			if( cnc.IsCancellationRequested ) return;

			List<Segment> segments_to_underline = null;

			if( is_focussed )
			{
				segments_to_underline = GetUnderliningInfo( cnc, td, matches, show_captures ).ToList( );
			}

			if( cnc.IsCancellationRequested ) return;

			LocalUnderliningAdorner.SetRangesToUnderline(
							segments_to_underline
								?.Select( s => (td.SafeGetPointer( s.Index ), td.SafeGetPointer( s.Index + s.Length )) )
								?.ToList( ) );

			if( is_focussed )
			{
				if( cnc.IsCancellationRequested ) return;

				ChangeEventHelper.BeginInvoke( CancellationToken.None, ( ) =>
							{
								LocalUnderliningFinished?.Invoke( this, null );
							} );
			}
		}


		void ExternalUnderliningThreadProc( ICancellable cnc )
		{
			string eol;
			IReadOnlyList<Segment> segments;
			bool set_selection;

			lock( this )
			{
				eol = LastEol;
				segments = LastExternalUnderliningSegments;
				set_selection = LastExternalUnderliningSetSelection;
			}

			TextData td = null;

			ChangeEventHelper.Invoke( CancellationToken.None, ( ) =>
			{
				td = rtb.GetTextData( eol );
			} );

			if( cnc.IsCancellationRequested ) return;

			ExternalUnderliningAdorner.SetRangesToUnderline(
							segments
								?.Select( s => (td.SafeGetPointer( s.Index ), td.SafeGetPointer( s.Index + s.Length )) )
								?.ToList( ) );

			if( cnc.IsCancellationRequested ) return;

			if( segments?.Count > 0 )
			{
				ChangeEventHelper.Invoke( CancellationToken.None, ( ) =>
				{
					var first = segments.First( );

					var r = td.Range( first.Index, first.Length );

					switch( r.Start.Parent )
					{
					case FrameworkContentElement fce:
						fce.BringIntoView( );
						break;
					case FrameworkElement fe:
						fe.BringIntoView( );
						break;
					}

					if( set_selection && !rtb.IsKeyboardFocused )
					{
						TextPointer p = r.Start.GetInsertionPosition( LogicalDirection.Forward );
						rtb.Selection.Select( p, p );
					}
				} );
			}
		}


		static IReadOnlyList<Segment> GetUnderliningInfo( ICancellable reh, TextData td, IReadOnlyList<Match> matches, bool showCaptures )
		{
			var items = new List<Segment>( );

			// include captures and groups; if no such objects, then include matches

			foreach( var match in matches )
			{
				if( reh.IsCancellationRequested ) break;

				if( !match.Success ) continue;

				bool found = false;

				foreach( Group group in match.Groups.Cast<Group>( ).Skip( 1 ) )
				{
					if( reh.IsCancellationRequested ) break;

					if( !group.Success ) continue;

					if( showCaptures )
					{
						foreach( Capture capture in group.Captures )
						{
							if( reh.IsCancellationRequested ) break;

							if( td.SelectionStart >= capture.Index && td.SelectionStart <= capture.Index + capture.Length )
							{
								items.Add( new Segment( capture.Index, capture.Length ) );
								found = true;
							}
						}
					}

					if( td.SelectionStart >= group.Index && td.SelectionStart <= group.Index + group.Length )
					{
						items.Add( new Segment( group.Index, group.Length ) );
						found = true;
					}
				}

				if( !found )
				{
					if( td.SelectionStart >= match.Index && td.SelectionStart <= match.Index + match.Length )
					{
						items.Add( new Segment( match.Index, match.Length ) );
					}
				}
			}

			return items;
		}


		[Conditional( "DEBUG" )]
		private void ShowDebugInformation( )
		{
			string s = "";

			TextPointer start = rtb.Selection.Start;

			Rect rectB = start.GetCharacterRect( LogicalDirection.Backward );
			Rect rectF = start.GetCharacterRect( LogicalDirection.Forward );

			s += $"BPos: {(int)rectB.Left}×{rectB.Bottom}, FPos: {(int)rectF.Left}×{rectF.Bottom}";

			char[] bc = new char[1];
			char[] fc = new char[1];

			int bn = start.GetTextInRun( LogicalDirection.Backward, bc, 0, 1 );
			int fn = start.GetTextInRun( LogicalDirection.Forward, fc, 0, 1 );

			s += $", Bc: '{( bn == 0 ? '∅' : bc[0] )}', Fc: '{( fn == 0 ? '∅' : fc[0] )}";

			lblDbgInfo.Content = s;
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
					using( LocalUnderliningLoop ) { }
					using( ExternalUnderliningLoop ) { }
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~UCText()
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
