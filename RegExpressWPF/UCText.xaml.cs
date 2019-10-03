﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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
		IReadOnlyList<Match> LastMatches; // null if no data, or recolouring process is not finished
		bool LastShowCaptures;
		string LastEol;

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

			lock( this )
			{
				if( LastMatches != null )
				{
					var old_groups = LastMatches.SelectMany( m => m.Groups.Cast<Group>( ) ).Select( g => (g.Index, g.Length, g.Value) );
					var new_groups = matches.SelectMany( m => m.Groups.Cast<Group>( ) ).Select( g => (g.Index, g.Length, g.Value) );

					if( new_groups.SequenceEqual( old_groups ) &&
						showCaptures == LastShowCaptures &&
						eol == LastEol )
					{
						LastMatches = matches;

						return;
					}
				}
			}

			RecolouringTask.Stop( );
			UnderliningTask.Stop( );

			LastMatches = null;
			LastEol = null;

			RestartRecolouring( matches, showCaptures, eol );
		}


		public void ShowWhiteSpaces( bool yes )
		{
			WhitespaceAdorner.ShowWhiteSpaces( yes );
		}


		public IReadOnlyList<Segment> GetUnderliningInfo( )
		{
			lock( this )
			{
				if( LastMatches == null ) // no data or processes not finished
				{
					return Enumerable.Empty<Segment>( ).ToList( );
				}
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


		public void SetUnderlinedCaptures( IReadOnlyList<Segment> segments, bool setSelection )
		{
			lock( this )
			{
				if( LastMatches != null ) RestartExternalUnderlining( segments, LastEol, setSelection );
			}
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

			lock( this )
			{
				if( LastMatches != null ) RestartLocalUnderlining( LastMatches, LastShowCaptures, LastEol );
			}

			UndoRedoHelper.HandleSelectionChanged( );

			SelectionChanged?.Invoke( this, null );
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


		private void Rtb_GotFocus( object sender, RoutedEventArgs e )
		{
			lock( this )
			{
				if( LastMatches != null ) RestartLocalUnderlining( LastMatches, LastShowCaptures, LastEol );
			}

			{
				//var rect = rtb.Selection.Start.GetCharacterRect( LogicalDirection.Forward );
				//rect.Width = 10;
				//rtb.BringIntoView( rect);
				var p = rtb.Selection?.Start?.Parent as FrameworkContentElement;
				if( p != null )
				{
					p.BringIntoView( );
				}
			}
		}


		private void Rtb_LostFocus( object sender, RoutedEventArgs e )
		{
			lock( this )
			{
				if( LastMatches != null ) RestartLocalUnderlining( Enumerable.Empty<Match>( ).ToList( ), LastShowCaptures, LastEol );
			}
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
			rtb.Focus( );

			var r = new TextRange( rtb.Document.ContentStart, rtb.Document.ContentEnd );

			using( var fs = File.OpenWrite( @"debug-uctext.xml" ) )
			{
				r.Save( fs, DataFormats.Xaml, true );
			}
		}


		void RestartRecolouring( IReadOnlyList<Match> matches, bool showCaptures, string eol )
		{
			RecolouringTask.Restart( ct => RecolourTaskProc( ct, matches, showCaptures, eol ) );

			if( rtb.IsFocused )
			{
				RestartLocalUnderlining( matches, showCaptures, eol ); // (started as a continuation of previous 'RecolouringTask')
			}
		}


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void RecolourTaskProc( CancellationToken ct, IReadOnlyList<Match> matches, bool showCaptures, string eol )
		{
			try
			{
				if( ct.WaitHandle.WaitOne( 333 ) ) return;
				ct.ThrowIfCancellationRequested( );

				Debug.WriteLine( $"START RECOLOURING" );
				DateTime start_time = DateTime.UtcNow;

				TextData td = null;

				ChangeEventHelper.Invoke( ct, ( ) =>
				{
					td = rtb.GetTextData( eol );
					pbProgress.Maximum = matches.Count;
					pbProgress.Value = 0;
				} );

				ct.ThrowIfCancellationRequested( );

				// (NOTE. Overlaps are possible in this example: (?=(..))

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

				ChangeEventHelper.BeginInvoke( ct, ( ) =>
				{
					pbProgress.Visibility = Visibility.Hidden;
				} );


				Debug.WriteLine( $"TEXT RECOLOURED: {( DateTime.UtcNow - start_time ).TotalMilliseconds:#,##0}" );

				lock( this )
				{
					LastMatches = matches;
					LastShowCaptures = showCaptures;
					LastEol = eol;
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

#if false
						if( setSelection && !rtb.IsKeyboardFocused )
						{
							TextPointer p = r.Start.GetInsertionPosition( LogicalDirection.Forward );
							rtb.Selection.Select( p, p );
						}
#endif
					} );
				}

				Debug.WriteLine( $"TEXT UNDERLINED: {( DateTime.UtcNow - start_time ).TotalMilliseconds:#,##0}" );
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
