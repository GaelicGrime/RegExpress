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

		readonly Thread RecolouringThread;
		readonly Thread LocalUnderliningThread;
		readonly Thread ExternalUnderliningThread;

		readonly RestartEvents RecolouringEvents = new RestartEvents( );
		readonly RestartEvents LocalUnderliningEvents = new RestartEvents( );
		readonly RestartEvents ExternalUnderliningEvents = new RestartEvents( );

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


			RecolouringThread = new Thread( RecolouringThreadProc )
			{
				IsBackground = true,
				Priority = ThreadPriority.BelowNormal,
			};
			RecolouringThread.Start( );

			LocalUnderliningThread = new Thread( LocalUnderliningThreadProc )
			{
				IsBackground = true,
				Priority = ThreadPriority.BelowNormal,
			};
			LocalUnderliningThread.Start( );

			ExternalUnderliningThread = new Thread( ExternalUnderliningThreadProc )
			{
				IsBackground = true,
				Priority = ThreadPriority.BelowNormal,
			};
			ExternalUnderliningThread.Start( );


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

			RecolouringEvents.SendStop( );
			LocalUnderliningEvents.SendStop( );
			ExternalUnderliningEvents.SendStop( );

			lock( this )
			{
				LastMatches = matches;
				LastShowCaptures = showCaptures;
				LastEol = eol;
				LastExternalUnderliningSegments = null;
			}

			RecolouringEvents.SendRestart( );
			LocalUnderliningEvents.SendRestart( );
			ExternalUnderliningEvents.SendRestart( );
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

			return GetUnderliningInfo( RestartEventsHelper.NonCancellable, td, LastMatches, LastShowCaptures );
		}


		public void SetExternalUnderlining( IReadOnlyList<Segment> segments, bool setSelection )
		{
			ExternalUnderliningEvents.SendStop( );

			lock( this )
			{
				LastExternalUnderliningSegments = segments;
				LastExternalUnderliningSetSelection = setSelection;
			}

			ExternalUnderliningEvents.SendRestart( );
		}


		public void StopAll( )
		{
			RecolouringEvents.SendStop( );
			LocalUnderliningEvents.SendStop( );
			ExternalUnderliningEvents.SendStop( );
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

			LocalUnderliningEvents.SendRestart( );

			UndoRedoHelper.HandleSelectionChanged( );

			SelectionChanged?.Invoke( this, null );

			ShowDebugInformation( ); // #if DEBUG
		}


		private void rtb_TextChanged( object sender, TextChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			RecolouringEvents.SendStop( );
			LocalUnderliningEvents.SendStop( );
			ExternalUnderliningEvents.SendStop( );

			UndoRedoHelper.HandleTextChanged( e );

			lock( this )
			{
				LastMatches = null;
				LastShowCaptures = false;
				LastEol = null;
			}

			TextChanged?.Invoke( this, null );
		}


		private void rtb_ScrollChanged( object sender, ScrollChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			RecolouringEvents.SendRestart( );
		}


		private void rtb_SizeChanged( object sender, SizeChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			RecolouringEvents.SendStop( );
		}


		private void rtb_GotFocus( object sender, RoutedEventArgs e )
		{
			LocalUnderliningEvents.SendRestart( );
			ExternalUnderliningEvents.SendRestart( );

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
			LocalUnderliningEvents.SendRestart( );
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


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void RecolouringThreadProc( )
		{
			var reh = RecolouringEvents.BuildHelper( );
			try
			{
				for(; ; )
				{
					// TODO: consider things related to termination

					reh.WaitInfinite( );

					if( reh.IsStopRequested ) continue;
					if( !reh.IsRestartRequested ) { Debug.Assert( false ); continue; }

					for(; ; )
					{
						reh.WaitForSilence( 333, 555 );

						if( reh.IsStopRequested ) break;
						if( reh.IsRestartRequested ) continue;

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

							if( reh.IsAnyRequested ) return;

							td = td0;
							clip_rect = new Rect( new Size( rtb.ViewportWidth, rtb.ViewportHeight ) );

							TextPointer top_pointer = rtb.GetPositionFromPoint( new Point( 0, 0 ), snapToText: true ).GetLineStartPosition( -1, out int _ );
							if( reh.IsAnyRequested ) return;

							top_index = RtbUtilities.FindNearestBefore( td.Pointers, top_pointer );
							if( reh.IsAnyRequested ) return;
							if( top_index < 0 ) top_index = 0;

							TextPointer bottom_pointer = rtb.GetPositionFromPoint( new Point( 0, rtb.ViewportHeight ), snapToText: true ).GetLineStartPosition( +1, out int lines_skipped );
							if( reh.IsAnyRequested ) return;

							// (Note. Last pointer from 'td.Pointers' is reserved for end-of-document)
							if( bottom_pointer == null || lines_skipped == 0 )
							{
								bottom_index = td.Pointers.Count - 2;
							}
							else
							{
								bottom_index = RtbUtilities.FindNearestAfter( td.Pointers, bottom_pointer );
								if( reh.IsAnyRequested ) return;
							}
							if( bottom_index >= td.Pointers.Count - 1 ) bottom_index = td.Pointers.Count - 2;
							if( bottom_index < top_index ) bottom_index = top_index; // (including 'if bottom_index == 0')
						} );

						if( reh.IsStopRequested ) break;
						if( reh.IsRestartRequested ) continue;

						if( td == null ) break;
						if( td.Text.Length == 0 ) break;

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
								if( reh.IsAnyRequested ) break;

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

						if( reh.IsStopRequested ) break;
						if( reh.IsRestartRequested ) continue;

						List<(Segment segment, StyleInfo styleInfo)> segments_to_uncolour =
							coloured_ranges
								.GetSegments( reh, false, top_index )
								.Select( s => (s, NormalStyleInfo) )
								.ToList( );

						if( reh.IsStopRequested ) break;
						if( reh.IsRestartRequested ) continue;

						int center_index = ( top_index + bottom_index ) / 2;

						var all_segments_and_styles =
							segments_and_styles.Concat( segments_to_uncolour )
							.OrderBy( s => Math.Abs( center_index - ( s.segment.Index + s.segment.Length / 2 ) ) )
							.ToList( );

						if( reh.IsStopRequested ) break;
						if( reh.IsRestartRequested ) continue;

						RtbUtilities.ApplyStyle( reh, ChangeEventHelper, pbProgress, td, all_segments_and_styles );

						if( reh.IsStopRequested ) break;
						if( reh.IsRestartRequested ) continue;

						UITaskHelper.BeginInvoke( pbProgress, ( ) =>
						{
							pbProgress.Visibility = Visibility.Hidden;
						} );


						break;
					}
				}
			}
			catch( OperationCanceledException ) // also 'TaskCanceledException'
			{
				// ignore
			}
			catch( ThreadInterruptedException )
			{
				// ignore
			}
			catch( ThreadAbortException )
			{
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
		void LocalUnderliningThreadProc( )
		{
			var reh = LocalUnderliningEvents.BuildHelper( );
			try
			{
				for(; ; )
				{
					// TODO: consider things related to termination

					reh.WaitInfinite( );

					if( reh.IsStopRequested ) continue;
					if( !reh.IsRestartRequested ) { Debug.Assert( false ); continue; }

					for(; ; )
					{
						reh.WaitForSilence( 222, 444 );

						if( reh.IsStopRequested ) break;
						if( reh.IsRestartRequested ) continue;

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

						if( matches == null ) break;

						TextData td = null;

						ChangeEventHelper.Invoke( CancellationToken.None, ( ) =>
						{
							is_focussed = rtb.IsFocused;
							if( is_focussed ) td = rtb.GetTextData( eol );
						} );

						if( reh.IsStopRequested ) break;
						if( reh.IsRestartRequested ) continue;

						List<Segment> segments_to_underline = null;

						if( is_focussed )
						{
							segments_to_underline = GetUnderliningInfo( reh, td, matches, show_captures ).ToList( );
						}

						if( reh.IsStopRequested ) break;
						if( reh.IsRestartRequested ) continue;

						LocalUnderliningAdorner.SetRangesToUnderline(
							segments_to_underline
								?.Select( s => (td.SafeGetPointer( s.Index ), td.SafeGetPointer( s.Index + s.Length )) )
								?.ToList( ) );

						if( is_focussed )
						{
							if( reh.IsStopRequested ) break;
							if( reh.IsRestartRequested ) continue;

							ChangeEventHelper.BeginInvoke( CancellationToken.None, ( ) =>
							{
								LocalUnderliningFinished?.Invoke( this, null );
							} );
						}


						break;
					}
				}
			}
			catch( OperationCanceledException ) // also 'TaskCanceledException'
			{
				// ignore
			}
			catch( ThreadInterruptedException )
			{
				// ignore
			}
			catch( ThreadAbortException )
			{
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
		void ExternalUnderliningThreadProc( )
		{
			var reh = ExternalUnderliningEvents.BuildHelper( );
			try
			{
				for(; ; )
				{
					// TODO: consider things related to termination

					reh.WaitInfinite( );

					if( reh.IsStopRequested ) continue;
					if( !reh.IsRestartRequested ) { Debug.Assert( false ); continue; }

					for(; ; )
					{
						reh.WaitForSilence( 333, 555 );

						if( reh.IsStopRequested ) break;
						if( reh.IsRestartRequested ) continue;

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

						if( reh.IsStopRequested ) break;
						if( reh.IsRestartRequested ) continue;

						ExternalUnderliningAdorner.SetRangesToUnderline(
							segments
								?.Select( s => (td.SafeGetPointer( s.Index ), td.SafeGetPointer( s.Index + s.Length )) )
								?.ToList( ) );

						if( reh.IsStopRequested ) break;
						if( reh.IsRestartRequested ) continue;

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


						break;
					}
				}
			}
			catch( OperationCanceledException ) // also 'TaskCanceledException'
			{
				// ignore
			}
			catch( ThreadInterruptedException )
			{
				// ignore
			}
			catch( ThreadAbortException )
			{
				// ignore
			}
			catch( Exception exc )
			{
				_ = exc;
				if( Debugger.IsAttached ) Debugger.Break( );
				throw;
			}
		}


		static IReadOnlyList<Segment> GetUnderliningInfo( ICancellable reh, TextData td, IReadOnlyList<Match> matches, bool showCaptures )
		{
			var items = new List<Segment>( );

			// include captures and groups; if no such objects, then include matches

			foreach( var match in matches )
			{
				if( reh.IsCancelRequested ) break;

				if( !match.Success ) continue;

				bool found = false;

				foreach( Group group in match.Groups.Cast<Group>( ).Skip( 1 ) )
				{
					if( reh.IsCancelRequested ) break;

					if( !group.Success ) continue;

					if( showCaptures )
					{
						foreach( Capture capture in group.Captures )
						{
							if( reh.IsCancelRequested ) break;

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

					using( RecolouringEvents ) { }
					using( LocalUnderliningEvents ) { }
					using( ExternalUnderliningEvents ) { }
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
