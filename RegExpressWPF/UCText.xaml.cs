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
		IReadOnlyList<Match> Matches;
		bool ShowCaptures;
		string Eol;

		readonly StyleInfo[] HighlightStyleInfos;

		readonly LengthConverter LengthConverter = new LengthConverter( );


		public event EventHandler TextChanged;
		public event EventHandler SelectionChanged;
		public event EventHandler LocalUnderliningFinished;


		public UCText( )
		{
			InitializeComponent( );

			WhitespaceAdorner = new WhitespaceAdorner( rtb );
			UnderliningAdorner = new UnderliningAdorner( rtb );

			ChangeEventHelper = new ChangeEventHelper( this.rtb );
			UndoRedoHelper = new UndoRedoHelper( this.rtb );

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
		}


		public string GetText( string eol )
		{
			var td = rtb.GetTextData( eol );

			return td.Text;
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


		public void SetMatches( IReadOnlyList<Match> matches, bool showCaptures, string eol )
		{
			if( matches == null ) throw new ArgumentNullException( nameof( matches ) );

			//.........
			//lock( this )
			//{
			//	if( Matches != null )
			//	{
			//		var old_groups = Matches.SelectMany( m => m.Groups.Cast<Group>( ) ).Select( g => (g.Index, g.Length, g.Value) );
			//		var new_groups = matches.SelectMany( m => m.Groups.Cast<Group>( ) ).Select( g => (g.Index, g.Length, g.Value) );

			//		if( new_groups.SequenceEqual( old_groups ) &&
			//			showCaptures == ShowCaptures &&
			//			eol == Eol )
			//		{
			//			// nothing changed, but keep new object

			//			Matches = matches;
			//			return;
			//		}
			//	}
			//}

			RecolouringTask.Stop( );
			UnderliningTask.Stop( );

			lock( this )
			{
				Matches = matches;
				ShowCaptures = showCaptures;
				Eol = eol;
			}

			RestartRecolouring( );
		}


		public void ShowWhiteSpaces( bool yes )
		{
			WhitespaceAdorner.ShowWhiteSpaces( yes );
		}


		public IReadOnlyList<Segment> GetUnderliningInfo( )
		{
			if( Matches == null )
			{
				return Enumerable.Empty<Segment>( ).ToList( );
			}

			TextData td = null;

			if( !CheckAccess( ) )
			{
				ChangeEventHelper.Invoke( CancellationToken.None, ( ) =>
				{
					td = rtb.GetTextData( Eol );
				} );
			}
			else
			{
				td = rtb.GetTextData( Eol );
			}

			// Note. 'Matches' could be null

			return GetUnderliningInfo( CancellationToken.None, td, Matches, ShowCaptures );
		}


		public void SetExternalUnderlining( IReadOnlyList<Segment> segments, bool setSelection )
		{
			RestartExternalUnderlining( segments, Eol, setSelection );
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

			RestartLocalUnderlining( Matches, ShowCaptures, Eol );

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

			TextChanged?.Invoke( this, null );
		}


		private void Rtb_ScrollChanged( object sender, ScrollChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			RecolouringTask.Restart( ct => RecolourTaskProc( ct, Matches, ShowCaptures, Eol ) );
		}

		private void Rtb_SizeChanged( object sender, SizeChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			//...RestartRecolouring( );
			RecolouringTask.Restart( ct => RecolourTaskProc( ct, Matches, ShowCaptures, Eol ) );
		}


		private void Rtb_GotFocus( object sender, RoutedEventArgs e )
		{
			RestartLocalUnderlining( Matches, ShowCaptures, Eol );

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
			RestartLocalUnderlining( Enumerable.Empty<Match>( ).ToList( ), ShowCaptures, Eol );
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

			var r = new TextRange( rtb.Document.ContentStart, rtb.Document.ContentEnd );

			using( var fs = File.OpenWrite( @"debug-uctext.xml" ) )
			{
				r.Save( fs, DataFormats.Xaml, true );
			}

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

		void RestartRecolouring( )
		{
			RecolouringTask.Restart( ct => RecolourTaskProc( ct, Matches, ShowCaptures, Eol ) );

			if( rtb.IsFocused )
			{
				RestartLocalUnderlining( Matches, ShowCaptures, Eol );
			}
		}


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void RecolourTaskProc( CancellationToken ct, IReadOnlyList<Match> matches, bool showCaptures, string eol )
		{
			try
			{
				if( ct.WaitHandle.WaitOne( 333 ) ) return;
				ct.ThrowIfCancellationRequested( );

				TextData td = null;
				TextPointer start_pointer = null;
				TextPointer end_pointer = null;

				ChangeEventHelper.Invoke( ct, ( ) =>
				{
					td = rtb.GetTextData( eol );
					start_pointer = rtb.GetPositionFromPoint( new Point( 0, 0 ), snapToText: true ).GetLineStartPosition( 0, out int skipped );
					var end_pointer_right = rtb.GetPositionFromPoint( new Point( rtb.ViewportWidth, rtb.ViewportHeight ), snapToText: true );
					var end_pointer_left = rtb.GetPositionFromPoint( new Point( 0, rtb.ViewportHeight ), snapToText: true );
					if( end_pointer_left.CompareTo( end_pointer_right ) > 0 )
					{
						end_pointer = end_pointer_left;
					}
					else
					{
						end_pointer = end_pointer_right;
					}
					var next = end_pointer.GetLineStartPosition( +1 );
					if( next != null ) end_pointer = next;

					//............pbProgress.Maximum = matches.Count;
					pbProgress.Value = 0;
				} );

				ct.ThrowIfCancellationRequested( );

				int start_index = RtbUtilities.FindNearestBefore( td.Pointers, start_pointer );
				int end_index = RtbUtilities.FindNearestAfter( td.Pointers, end_pointer );
				if( start_index < 0 ) start_index = 0;
				if( end_index < 0 ) end_index = td.Pointers.Count - 1;
				int length = end_index - start_index;

				if( length != 0 ) // (zero in case of empty text)
				{
					// (NOTE. Overlaps are possible in this example: (?=(..))

					//............progress bar

					NaiveRanges all_ranges_to_colour = new NaiveRanges( length );

					if( matches != null )
					{
						int colour_count = HighlightStyleInfos.Length;

						NaiveRanges[] ranges_to_colour = new NaiveRanges[colour_count];
						for( int i = 0; i < ranges_to_colour.Length; ++i ) ranges_to_colour[i] = new NaiveRanges( length );

						for( int i = 0; i < matches.Count; ++i )
						{
							ct.ThrowIfCancellationRequested( );

							Match match = matches[i];

							if( match.Index + match.Length < start_index ) continue; // not visible yet
							if( match.Index > end_index ) break; // already invisible; assuming that the matches are ordered

							int colour_index = i % colour_count;
							var current_ranges = ranges_to_colour[colour_index];

							current_ranges.SafeSet( match.Index - start_index, match.Length );
							all_ranges_to_colour.SafeSet( match.Index - start_index, match.Length );
						}

						for( int i = 0; i < colour_count; ++i )
						{
							ct.ThrowIfCancellationRequested( );

							var style = HighlightStyleInfos[i];
							var ranges = ranges_to_colour[i];
							var segments = ranges.GetSegments( ct, valuesToInclude: true )
								.Select( s => new Segment( s.Index + start_index, s.Length ) )
								.ToList( );

							RtbUtilities.ApplyStyle( ct, ChangeEventHelper, pbProgress, td, segments, style );
						}
					}

					//RtbUtilities.ApplyStyle( ct, ChangeEventHelper, pbProgress, td, all_ranges_to_colour.GetSegments( ct, true ).ToList( ), ? );
					var segments_to_discolour = all_ranges_to_colour.GetSegments( ct, valuesToInclude: false )
						.Select( s => new Segment( s.Index + start_index, s.Length ) )
						.ToList( );
					RtbUtilities.ClearProperties( ct, ChangeEventHelper, pbProgress, td, segments_to_discolour );
				}

				/*
				var highlighted_ranges = new NaiveRanges( td.Text.Length );
				var segments_and_styles = new List<(Segment segment, StyleInfo styleInfo)>( );

				for( int i = 0; i < matches.Count; ++i )
				{
					ct.ThrowIfCancellationRequested( );

					var highlight_index = unchecked(i % HighlightStyleInfos.Length);
					Match match = matches[i];
					Debug.Assert( match.Success );

					highlighted_ranges.Set( match.Index, match.Length );
					segments_and_styles.Add( (new Segment( match.Index, match.Length ), HighlightStyleInfos[highlight_index]) );
				}


				RtbUtilities.ApplyStyle( ct, ChangeEventHelper, pbProgress, td, segments_and_styles );

				Debug.WriteLine( $"MATCHES COLOURED" );

				ct.ThrowIfCancellationRequested( );

				var unhighlighted_segments = highlighted_ranges.GetSegments( ct, false ).ToList( );

				RtbUtilities.ClearProperties( ct, ChangeEventHelper, pbProgress, td, unhighlighted_segments );

				Debug.WriteLine( $"NO-MATCHES COLOURED" );
				*/

				ChangeEventHelper.BeginInvoke( ct, ( ) =>
				{
					pbProgress.Visibility = Visibility.Hidden;
				} );
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


		void RestartLocalUnderlining( IReadOnlyList<Match> matches, bool showCaptures, string eol )
		{
			UnderliningTask.Restart( ct => LocalUnderlineTaskProc( ct, matches, showCaptures, eol ) );
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


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void ExternalUnderlineTaskProc( CancellationToken ct, IReadOnlyList<Segment> segments, string eol, bool setSelection )
		{
			try
			{
				if( ct.WaitHandle.WaitOne( 333 ) ) return;
				ct.ThrowIfCancellationRequested( );

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


		static IReadOnlyList<Segment> GetUnderliningInfo( CancellationToken ct, TextData td, IReadOnlyList<Match> matches, bool showCaptures )
		{
			var items = new List<Segment>( );

			if( matches != null )
			{

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
