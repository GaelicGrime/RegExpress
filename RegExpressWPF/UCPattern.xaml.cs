using System;
using System.Collections.Generic;
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

		readonly TaskHelper RecolouringTask = new TaskHelper( );

		readonly ChangeEventHelper ChangeEventHelper;
		readonly UndoRedoHelper UndoRedoHelper;

		bool AlreadyLoaded = false;

		readonly StyleInfo PatternParaHighlightStyleInfo;
		readonly StyleInfo PatternEscapeStyleInfo;
		readonly StyleInfo PatternGroupHighlightStyleInfo;
		readonly StyleInfo CommentStyleInfo;

		RegexOptions mRegexOptions;

		public event EventHandler TextChanged;


		public UCPattern( )
		{
			InitializeComponent( );

			WhitespaceAdorner = new WhitespaceAdorner( rtb );

			ChangeEventHelper = new ChangeEventHelper( this.rtb );
			UndoRedoHelper = new UndoRedoHelper( this.rtb );

			PatternParaHighlightStyleInfo = new StyleInfo( "PatternParaHighlight" );
			PatternEscapeStyleInfo = new StyleInfo( "PatternEscape" );
			PatternGroupHighlightStyleInfo = new StyleInfo( "PatternGroupHighlight" );
			CommentStyleInfo = new StyleInfo( "PatternComment" );
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


		public void SetRegexOptions( RegexOptions regexOptions )
		{
			StopAll( );
			mRegexOptions = regexOptions;
			if( IsLoaded ) RestartRecolouring( );
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
			RecolouringTask.Stop( );
		}


		private void UserControl_Loaded( object sender, RoutedEventArgs e )
		{
			if( AlreadyLoaded ) return;

			var adorner_layer = AdornerLayer.GetAdornerLayer( rtb );
			adorner_layer.Add( WhitespaceAdorner );

			AlreadyLoaded = true;
		}


		private void Rtb_SelectionChanged( object sender, RoutedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;
			if( !rtb.IsFocused ) return;

			UndoRedoHelper.HandleSelectionChanged( );

			RestartRecolouring( );
		}


		private void Rtb_TextChanged( object sender, TextChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			UndoRedoHelper.HandleTextChanged( );
			RestartRecolouring( );
			TextChanged?.Invoke( this, null );
		}


		private void Rtb_GotFocus( object sender, RoutedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			RestartRecolouring( );

			if( Properties.Settings.Default.BringCaretIntoView )
			{
				var p = rtb.CaretPosition?.Parent as FrameworkContentElement;
				if( p != null )
				{
					p.BringIntoView( );
				}
			}
		}


		private void Rtb_LostFocus( object sender, RoutedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			RestartRecolouring( );
		}


		private void Rtb_Copying( object sender, DataObjectCopyingEventArgs e )
		{
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


		void RestartRecolouring( )
		{
			bool is_focused = rtb.IsFocused;

			RecolouringTask.Restart( ct => RecolourTaskProc( ct, is_focused, mRegexOptions ) );
		}


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void RecolourTaskProc( CancellationToken ct, bool is_focused, RegexOptions regexOptions )
		{
			try
			{
				if( ct.WaitHandle.WaitOne( 333 ) ) return;
				ct.ThrowIfCancellationRequested( );

				TextData td = null;

				ChangeEventHelper.Invoke( ct, ( ) =>
				{
					td = rtb.GetTextData( null );
				} );

				ct.ThrowIfCancellationRequested( );

				var coloured_ranges = new NaiveRanges( td.Text.Length );

				bool ignore_pattern_whitespace = ( regexOptions & RegexOptions.IgnorePatternWhitespace ) != 0;

				string pattern = $@"
					(?'inline_comment'\(\?\#.*?(\)|$)) |
                    (?'para'\(|\)) |
                    (?'group'\[(\\.|.)*?(?'eog'\])) |
                    {( ignore_pattern_whitespace ? @"(?'eol_comment'\#[^\r\n]*) |" : "" )}
                    (?'other'(\\.|[^\(\)\[\]{( ignore_pattern_whitespace ? "#" : "" )}])+)
                    ";

				var matches = Regex.Matches( td.Text, pattern, RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline )
					.Cast<Match>( )
					.ToArray( );

				ct.ThrowIfCancellationRequested( );

				{
					// process comments

					List<Segment> comment_segments =
						matches
							.Select( m => m.Groups["inline_comment"] )
							.Concat( matches.Select( m => m.Groups["eol_comment"] ) )
							.Where( g => g.Success )
							.Select( g => new Segment( g.Index, g.Length ) )
							.ToList( );

					RtbUtilities.ApplyStyle( ct, ChangeEventHelper, null, td, comment_segments, CommentStyleInfo );

					coloured_ranges.Set( comment_segments );

#if false
					// highlighting '( )' of active comment too
					if( is_focused )
					{
						Group active_inline_comment =
							matches
								.Select( m => m.Groups["inline_comment"] )
								.FirstOrDefault( g => g.Success && td.SelectionStart >= g.Index && td.SelectionEnd < g.Index + g.Length );

						if( active_inline_comment != null )
						{
							ChangeEventHelper.BeginInvoke( ct, ( ) =>
							{
								td.Range( active_inline_comment.Index, 1 ).Style( PatternParaHighlightStyleInfo );
								td.Range( active_inline_comment.Index + active_inline_comment.Length - 1, 1 ).Style( PatternParaHighlightStyleInfo );
							} );

							coloured_ranges.Set( active_inline_comment.Index );
							coloured_ranges.Set( active_inline_comment.Index + active_inline_comment.Length - 1 );
						}
					}
#endif

				}

				if( is_focused )
				{
					var parentheses = matches.Where( m => m.Groups["para"].Success ).ToArray( );
					var parentheses_at_left = parentheses.Where( m => m.Index < td.SelectionStart ).ToArray( );
					var parentheses_at_right = parentheses.Where( m => m.Index >= td.SelectionStart ).ToArray( );

					ct.ThrowIfCancellationRequested( );

					if( parentheses_at_left.Any( ) )
					{
						int n = 0;
						int found_i = -1;
						for( int i = parentheses_at_left.Length - 1; i >= 0; --i )
						{
							ct.ThrowIfCancellationRequested( );

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

							ChangeEventHelper.BeginInvoke( ct, ( ) =>
							{
								td.Range( m.Index, 1 ).Style( PatternParaHighlightStyleInfo );
							} );

							coloured_ranges.Set( m.Index );
						}
					}

					if( parentheses_at_right.Any( ) )
					{
						int n = 0;
						int found_i = -1;
						for( int i = 0; i < parentheses_at_right.Length; ++i )
						{
							ct.ThrowIfCancellationRequested( );

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

							ChangeEventHelper.BeginInvoke( ct, ( ) =>
							{
								td.Range( m.Index, 1 ).Style( PatternParaHighlightStyleInfo );
							} );

							coloured_ranges.Set( m.Index );
						}
					}

					var current_group = matches.Where( m => m.Groups["group"].Success && m.Index <= td.SelectionStart && m.Index + m.Length >= td.SelectionStart ).FirstOrDefault( );
					if( current_group != null )
					{
						ct.ThrowIfCancellationRequested( );

						ChangeEventHelper.BeginInvoke( ct, ( ) =>
						{
							td.Range( current_group.Index, 1 ).Style( PatternGroupHighlightStyleInfo );
						} );

						coloured_ranges.Set( current_group.Index );

						var eog = current_group.Groups["eog"];
						if( eog.Success )
						{
							ChangeEventHelper.BeginInvoke( ct, ( ) =>
							{
								td.Range( eog.Index, 1 ).Style( PatternGroupHighlightStyleInfo );
							} );

							coloured_ranges.Set( eog.Index );
						}
					}
				}

				var escapes = new List<Segment>( );

				foreach( Match group in matches.Where( m => { var g = m.Groups["group"]; return g.Success && g.Length > 2; } ) )
				{
					ct.ThrowIfCancellationRequested( );
					GetRecolorEscapes( escapes, td, group.Index + 1, group.Length - 2, ct );
				}

				foreach( Match other in matches.Where( m => m.Groups["other"].Success ) )
				{
					ct.ThrowIfCancellationRequested( );
					GetRecolorEscapes( escapes, td, other.Index, other.Length, ct );
				}

				coloured_ranges.Set( escapes );

				{
					int i = 0;
					while( i < escapes.Count )
					{
						ct.ThrowIfCancellationRequested( );

						ChangeEventHelper.Invoke( ct, ( ) =>
						{
							var start = Environment.TickCount;
							var end = unchecked(start + 77); // (in rare case we will have overflow, which we are going to ignore)

							do
							{
								var e = escapes[i];
								td.Range( e ).Style( PatternEscapeStyleInfo );
							} while( ++i < escapes.Count && Environment.TickCount <= end );
						} );
					}
				}

				var segments_to_uncolour = coloured_ranges.GetSegments( ct, false ).ToList( );

				RtbUtilities.ClearProperties( ct, ChangeEventHelper, null, td, segments_to_uncolour );
					   //RtbUtilities.ApplyStyle( ct, UniqueChanger, null, td, segments_to_restore, PatternNormalStyleInfo );
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


		static void GetRecolorEscapes( List<Segment> list, TextData td, int start, int len, CancellationToken ct )
		{
			if( len == 0 ) return;

			string s = td.Text.Substring( start, len );

			const string pattern = @"\\[0-7]{2,3}|\\x[0-9A-F]{2}|\\c[A-Z]|\\u[0-9A-F]{4}|\\p\{[A-Z]+\}|\\k<[A-Z]+>|\\.";

			var ms = Regex.Matches( s, pattern, RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase );

			foreach( Match m in ms )
			{
				ct.ThrowIfCancellationRequested( );
				list.Add( new Segment( start + m.Index, m.Length ) );
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

					using( RecolouringTask ) { }
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
