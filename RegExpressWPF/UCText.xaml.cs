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
		readonly UnderliningAdorner UnderliningAdorner;

		readonly TaskHelper RecolouringTask = new TaskHelper( );
		readonly TaskHelper UnderliningTask = new TaskHelper( );

		readonly ChangeEventHelper ChangeEventHelper;
		readonly UndoRedoHelper UndoRedoHelper;

		bool AlreadyLoaded = false;
		IReadOnlyList<Match> LastMatches;
		bool LastShowCaptures;
		string LastEol;

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
			UnderliningAdorner = new UnderliningAdorner( rtb );

			NormalStyleInfo = new StyleInfo( "TextNormal" );

			HighlightStyleInfos = new[]
			{
				new StyleInfo( "MatchHighlight_0" ),
				new StyleInfo( "MatchHighlight_1" ),
				new StyleInfo( "MatchHighlight_2" )
			};

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
					}
					return;
				}
			}

			RecolouringTask.Stop( );
			UnderliningTask.Stop( );

			lock( this )
			{
				LastMatches = matches;
				LastShowCaptures = showCaptures;
				LastEol = eol;
			}

			RestartRecolouring( matches, showCaptures, eol );
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

			return GetUnderliningInfo( CancellationToken.None, td, LastMatches, LastShowCaptures );
		}


		public void SetExternalUnderlining( IReadOnlyList<Segment> segments, bool setSelection )
		{
			IReadOnlyList<Match> last_matches;
			//bool last_show_captures;
			string last_eol;

			lock( this )
			{
				last_matches = LastMatches;
				//last_show_captures = LastShowCaptures;
				last_eol = LastEol;
			}

			if( last_matches != null ) RestartExternalUnderlining( segments, last_eol, setSelection );
		}


		public void StopAll( )
		{
			RecolouringTask.Stop( );
			UnderliningTask.Stop( );
		}


		private void UserControl_Loaded( object sender, RoutedEventArgs e )
		{
			if( AlreadyLoaded ) return;

			rtb.Document.MinPageWidth = (double)LengthConverter.ConvertFromString( "21cm" );

			var adorner_layer = AdornerLayer.GetAdornerLayer( rtb );
			adorner_layer.Add( WhitespaceAdorner );
			adorner_layer.Add( UnderliningAdorner );

			AlreadyLoaded = true;
		}


		private void Rtb_SelectionChanged( object sender, RoutedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;
			if( !rtb.IsFocused ) return;

			IReadOnlyList<Match> last_matches;
			bool last_show_captures;
			string last_eol;

			lock( this )
			{
				last_matches = LastMatches;
				last_show_captures = LastShowCaptures;
				last_eol = LastEol;
			}

			if( last_matches != null ) RestartLocalUnderlining( last_matches, last_show_captures, last_eol );

			UndoRedoHelper.HandleSelectionChanged( );

			SelectionChanged?.Invoke( this, null );

			ShowDebugInformation( ); // #if DEBUG
		}


		private void Rtb_TextChanged( object sender, TextChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			RecolouringTask.Stop( );
			UnderliningTask.Stop( );

			UndoRedoHelper.HandleTextChanged( );

			LastMatches = null;
			LastEol = null;

			TextChanged?.Invoke( this, null );
		}


		private void rtb_ScrollChanged( object sender, ScrollChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			RecolouringTask.Stop( );
			UnderliningTask.Stop( ); //

			IReadOnlyList<Match> matches;
			bool show_captures;
			string eol;

			lock( this )
			{
				matches = LastMatches;
				show_captures = LastShowCaptures;
				eol = LastEol;
			}

			RestartRecolouring( matches, show_captures, eol );
		}


		private void rtb_SizeChanged( object sender, SizeChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			RecolouringTask.Stop( );
			UnderliningTask.Stop( ); //

			IReadOnlyList<Match> matches;
			bool show_captures;
			string eol;

			lock( this )
			{
				matches = LastMatches;
				show_captures = LastShowCaptures;
				eol = LastEol;
			}

			RestartRecolouring( matches, show_captures, eol );
		}


		private void Rtb_GotFocus( object sender, RoutedEventArgs e )
		{
			IReadOnlyList<Match> last_matches;
			bool last_show_captures;
			string last_eol;

			lock( this )
			{
				last_matches = LastMatches;
				last_show_captures = LastShowCaptures;
				last_eol = LastEol;
			}

			if( last_matches != null ) RestartLocalUnderlining( last_matches, last_show_captures, last_eol );

			if( Properties.Settings.Default.BringCaretIntoView )
			{
				var p = rtb.CaretPosition.Parent as FrameworkContentElement;
				if( p != null )
				{
					p.BringIntoView( );
				}
			}
		}


		private void Rtb_LostFocus( object sender, RoutedEventArgs e )
		{
			IReadOnlyList<Match> last_matches;
			bool last_show_captures;
			string last_eol;

			lock( this )
			{
				last_matches = LastMatches;
				last_show_captures = LastShowCaptures;
				last_eol = LastEol;
			}

			if( last_matches != null ) RestartLocalUnderlining( Enumerable.Empty<Match>( ).ToList( ), last_show_captures, last_eol );
		}


		private void Rtb_Pasting( object sender, DataObjectPastingEventArgs e )
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


		private void BtnDbgSave_Click( object sender, RoutedEventArgs e )
		{
#if DEBUG
			rtb.Focus( );

			Utilities.DbgSaveXAML( @"debug-uctext.xml", rtb.Document );

			SaveToPng( Window.GetWindow( this ), "debug-uctext.png" );
#endif
		}


		private void BtnDbgInsertB_Click( object sender, RoutedEventArgs e )
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

		private void BtnDbgInsertF_Click( object sender, RoutedEventArgs e )
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


		private void BtnDbgNextInsert_Click( object sender, RoutedEventArgs e )
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

		private void BtnDbgNextContext_Click( object sender, RoutedEventArgs e )
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

		void RestartRecolouring( IReadOnlyList<Match> matches, bool showCaptures, string eol )
		{
			RecolouringTask.Restart( ct => RecolouringTaskProc( ct, matches, showCaptures, eol ) );

			if( rtb.IsFocused )
			{
				RestartLocalUnderlining( matches, showCaptures, eol ); // (started as a continuation of previous 'RecolouringTask')
			}
		}


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void RecolouringTaskProc( CancellationToken ct, IReadOnlyList<Match> matches, bool showCaptures, string eol )
		{
			try
			{
				if( ct.WaitHandle.WaitOne( 333 ) ) return;
				ct.ThrowIfCancellationRequested( );

				Debug.WriteLine( $"START RECOLOURING" );
				DateTime start_time = DateTime.UtcNow;

				TextData td = null;
				Rect clip_rect = Rect.Empty;
				int top_index = 0;
				int bottom_index = 0;

				//...
				var t1 = Environment.TickCount;

				UITaskHelper.Invoke( rtb, ct, ( ) =>
				{
					td = null;

					var start_doc = rtb.Document.ContentStart;
					var end_doc = rtb.Document.ContentStart;

					if( !start_doc.HasValidLayout || !end_doc.HasValidLayout ) return;

					var td0 = rtb.GetTextData( eol );
					if( !td0.Pointers.Any( ) || !td0.Pointers[0].IsInSameDocument( start_doc ) ) return;

					td = td0;
					clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );

					TextPointer top_pointer = rtb.GetPositionFromPoint( new Point( 0, 0 ), snapToText: true ).GetLineStartPosition( -1, out int _ );
					top_index = RtbUtilities.FindNearestBefore( td.Pointers, top_pointer );
					if( top_index < 0 ) top_index = 0;

					TextPointer bottom_pointer = rtb.GetPositionFromPoint( new Point( 0, rtb.ViewportHeight ), snapToText: true ).GetLineStartPosition( +1, out int lines_skipped );
					// (Note. Last pointer from 'td.Pointers' is reserved for end-of-document)
					if( bottom_pointer == null || lines_skipped == 0 )
					{
						bottom_index = td.Pointers.Count - 2;
					}
					else
					{
						bottom_index = RtbUtilities.FindNearestAfter( td.Pointers, bottom_pointer );
					}
					if( bottom_index >= td.Pointers.Count - 1 ) bottom_index = td.Pointers.Count - 2;
					if( bottom_index < top_index ) bottom_index = top_index; // (including 'if bottom_index == 0')

					//...
					if( ct.IsCancellationRequested )
					{
						Debugger.Break( );
					}
				} );

				var t2 = Environment.TickCount;
				//Debug.WriteLine( $"Getting text: {t2 - t1:F0}" );

				if( td == null ) return;
				if( td.Text.Length == 0 ) return;

				ct.ThrowIfCancellationRequested( );

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
						ct.ThrowIfCancellationRequested( );

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

				List<(Segment segment, StyleInfo styleInfo)> segments_to_uncolour =
					coloured_ranges
						.GetSegments( ct, false, top_index )
						.Select( s => (s, NormalStyleInfo) )
						.ToList( );

				int center_index = ( top_index + bottom_index ) / 2;

				var all_segments_and_styles =
					segments_and_styles.Concat( segments_to_uncolour )
					.OrderBy( s => Math.Abs( center_index - ( s.segment.Index + s.segment.Length / 2 ) ) )
					.ToList( );

				RtbUtilities.ApplyStyle( ct, ChangeEventHelper, pbProgress, td, all_segments_and_styles );

				ChangeEventHelper.BeginInvoke( ct, ( ) =>
				{
					pbProgress.Visibility = Visibility.Hidden;
				} );

				Debug.WriteLine( $"TEXT RECOLOURED: {( DateTime.UtcNow - start_time ).TotalMilliseconds:#,##0}" );
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


		void RestartLocalUnderlining( IReadOnlyList<Match> matches, bool showCaptures, string eol )
		{
			UnderliningTask.RestartAfter( RecolouringTask, ct => LocalUnderlineTaskProc( ct, matches, showCaptures, eol ) );
		}


		void RestartExternalUnderlining( IReadOnlyList<Segment> segments, string eol, bool setSelection )
		{
			UnderliningTask.RestartAfter( RecolouringTask, ct => ExternalUnderlineTaskProc( ct, segments, eol, setSelection ) );
		}


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void LocalUnderlineTaskProc( CancellationToken ct, IReadOnlyList<Match> matches, bool showCaptures, string eol )
		{
			try
			{
				if( ct.WaitHandle.WaitOne( 222 ) ) return;
				ct.ThrowIfCancellationRequested( );

				Debug.WriteLine( $"START LOCAL UNDERLINING" );
				DateTime start_time = DateTime.UtcNow;

				TextData td = null;

				ChangeEventHelper.Invoke( ct, ( ) =>
				{
					td = rtb.GetTextData( eol );
				} );

				List<Segment> segments_to_underline = GetUnderliningInfo( ct, td, matches, showCaptures ).ToList( );

				UnderliningAdorner.SetRangesToUnderline(
					segments_to_underline
						.Select( s => (td.SafeGetPointer( s.Index ), td.SafeGetPointer( s.Index + s.Length )) )
						.ToList( ) );

				ChangeEventHelper.BeginInvoke( ct, ( ) =>
				{
					LocalUnderliningFinished?.Invoke( this, null );
				} );

				Debug.WriteLine( $"TEXT UNDERLINED: {( DateTime.UtcNow - start_time ).TotalMilliseconds:#,##0}" );
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


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void ExternalUnderlineTaskProc( CancellationToken ct, IReadOnlyList<Segment> segments, string eol, bool setSelection )
		{
			try
			{
				if( ct.WaitHandle.WaitOne( 333 ) ) return;
				ct.ThrowIfCancellationRequested( );

				Debug.WriteLine( $"START EXTERNAL UNDERLINING" );
				DateTime start_time = DateTime.UtcNow;

				TextData td = null;

				ChangeEventHelper.Invoke( ct, ( ) =>
				{
					td = rtb.GetTextData( eol );
				} );

				UnderliningAdorner.SetRangesToUnderline(
					segments
						.Select( s => (td.SafeGetPointer( s.Index ), td.SafeGetPointer( s.Index + s.Length )) )
						.ToList( ) );

				if( segments.Count > 0 )
				{
					ChangeEventHelper.Invoke( ct, ( ) =>
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

						if( setSelection && !rtb.IsKeyboardFocused )
						{
							TextPointer p = r.Start.GetInsertionPosition( LogicalDirection.Forward );
							rtb.Selection.Select( p, p );
						}
					} );
				}

				Debug.WriteLine( $"TEXT UNDERLINED: {( DateTime.UtcNow - start_time ).TotalMilliseconds:#,##0}" );
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


		static IReadOnlyList<Segment> GetUnderliningInfo( CancellationToken ct, TextData td, IReadOnlyList<Match> matches, bool showCaptures )
		{
			var items = new List<Segment>( );

			// include captures and groups; if no such objects, then include matches

			foreach( var match in matches )
			{
				if( !match.Success ) continue;

				bool found = false;

				foreach( Group group in match.Groups.Cast<Group>( ).Skip( 1 ) )
				{
					if( !group.Success ) continue;

					ct.ThrowIfCancellationRequested( );

					if( showCaptures )
					{
						foreach( Capture capture in group.Captures )
						{
							ct.ThrowIfCancellationRequested( );

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

					using( RecolouringTask ) { }
					using( UnderliningTask ) { }
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
