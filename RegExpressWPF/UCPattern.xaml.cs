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

		readonly StyleInfo PatternNormalStyleInfo;
		readonly StyleInfo PatternParaHighlightStyleInfo;
		readonly StyleInfo PatternGroupNameStyleInfo;
		readonly StyleInfo PatternEscapeStyleInfo;
		readonly StyleInfo PatternCharGroupHighlightStyleInfo;
		readonly StyleInfo PatternCommentStyleInfo;

		readonly Regex NoIgnorePatternWhitespaceRegex;
		readonly Regex IgnorePatternWhitespaceRegex;
		readonly Regex NamedGroupsRegex = new Regex( // (balancing groups covered too)
			@"\(\?(?'name'((?'a'')|<)\p{L}\w*(-\p{L}\w*)?(?(a)'|>))",
			RegexOptions.ExplicitCapture | RegexOptions.Compiled );
		readonly Regex EscapeRegex = new Regex(
			@"(?>\\[0-7]{2,3} | \\x[0-9A-F]{2} | \\c[A-Z] | \\u[0-9A-F]{4} | \\p\{[A-Z]+\} | \\k<[A-Z]+> | \\.)",
			RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace );


		RegexOptions mRegexOptions;
		string mEol;

		public event EventHandler TextChanged;


		public UCPattern( )
		{
			InitializeComponent( );

			NoIgnorePatternWhitespaceRegex =
				new Regex( BuildPattern( RegexOptions.None ),
					RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline );
			IgnorePatternWhitespaceRegex =
				new Regex( BuildPattern( RegexOptions.IgnorePatternWhitespace ),
					RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline );

			ChangeEventHelper = new ChangeEventHelper( this.rtb );
			UndoRedoHelper = new UndoRedoHelper( this.rtb );

			WhitespaceAdorner = new WhitespaceAdorner( rtb, ChangeEventHelper );

			PatternNormalStyleInfo = new StyleInfo( "PatternNormal" );
			PatternParaHighlightStyleInfo = new StyleInfo( "PatternParaHighlight" );
			PatternGroupNameStyleInfo = new StyleInfo( "PatternGroupName" );
			PatternEscapeStyleInfo = new StyleInfo( "PatternEscape" );
			PatternCharGroupHighlightStyleInfo = new StyleInfo( "PatternCharGroupHighlight" );
			PatternCommentStyleInfo = new StyleInfo( "PatternComment" );
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


		public void SetRegexOptions( RegexOptions regexOptions, string eol )
		{
			StopAll( );
			mRegexOptions = regexOptions;
			mEol = eol;
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


		private void rtb_SelectionChanged( object sender, RoutedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;
			if( !rtb.IsFocused ) return;

			UndoRedoHelper.HandleSelectionChanged( );

			//...RestartRecolouring( );
		}


		private void rtb_TextChanged( object sender, TextChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			UndoRedoHelper.HandleTextChanged( );
			RestartRecolouring( );
			TextChanged?.Invoke( this, null );
		}


		private void rtb_ScrollChanged( object sender, ScrollChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			RestartRecolouring( );
		}


		private void rtb_SizeChanged( object sender, SizeChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			RestartRecolouring( );
		}


		private void rtb_GotFocus( object sender, RoutedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			//...RestartRecolouring( );

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

			//...RestartRecolouring( );
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


		void RestartRecolouring( )
		{
			bool is_focused = rtb.IsFocused;

			RecolouringTask.Restart( ct => RecolouringTaskProc( ct, is_focused, mRegexOptions, mEol ) );
		}


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void RecolouringTaskProc( CancellationToken ct, bool is_focused, RegexOptions regexOptions, string eol )
		{
			try
			{
				if( ct.WaitHandle.WaitOne( 111 ) ) return;
				ct.ThrowIfCancellationRequested( );

				TextData td = null;
				Rect clip_rect = Rect.Empty;
				int top_index = 0;
				int bottom_index = 0;

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
					} );

				if( td == null ) return;

				ct.ThrowIfCancellationRequested( );

				Debug.Assert( top_index >= 0 );
				Debug.Assert( bottom_index >= top_index );
				Debug.Assert( bottom_index < td.Pointers.Count );

				var regex = GetColouringRegex( regexOptions );
				var coloured_ranges = new NaiveRanges( bottom_index - top_index + 1 );

				var matches = regex.Matches( td.Text )
					.Cast<Match>( )
					.ToArray( );

				ct.ThrowIfCancellationRequested( );


				//...
				var t1 = DateTime.Now;

				ColouriseComments( ct, td, coloured_ranges, clip_rect, top_index, bottom_index, matches );

				var t2 = DateTime.Now;
				Debug.WriteLine( "### Colouring comments: {0:F0}", ( t2 - t1 ).TotalMilliseconds );

				t1 = DateTime.Now;

				//...
				t1 = DateTime.Now;

				ColouriseEscapes( ct, td, coloured_ranges, clip_rect, top_index, bottom_index, matches );

				t2 = DateTime.Now;
				Debug.WriteLine( "### Colouring escapes: {0:F0}", ( t2 - t1 ).TotalMilliseconds );

				t1 = DateTime.Now;

				ColouriseNamedGroups( ct, td, coloured_ranges, clip_rect, top_index, bottom_index, matches );

				t2 = DateTime.Now;
				Debug.WriteLine( "### Colouring named groups: {0:F0}", ( t2 - t1 ).TotalMilliseconds );


				t1 = DateTime.Now;




#if false
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

				{
					// named groups

					var left_parentheses = matches.Select( m => m.Groups["left_para"] ).Where( g => g.Success ).ToList( );

					foreach( var lp in left_parentheses )
					{
						// (balancing groups covered too)

						var m = NamedGroupsRegex.Match( td.Text, lp.Index );
						if( m.Success )
						{
							var gn = m.Groups["name"];
							Debug.Assert( gn.Success );

							ChangeEventHelper.BeginInvoke( ct, ( ) =>
							{
								td.Range( gn.Index, gn.Length ).Style( PatternGroupNameStyleInfo );
							} );

							coloured_ranges.Set( gn.Index, gn.Length );
						}
					}
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

					var current_group = matches.Where( m => m.Groups["character_group"].Success && m.Index <= td.SelectionStart && m.Index + m.Length >= td.SelectionStart ).FirstOrDefault( );
					if( current_group != null )
					{
						ct.ThrowIfCancellationRequested( );

						ChangeEventHelper.BeginInvoke( ct, ( ) =>
						{
							td.Range( current_group.Index, 1 ).Style( PatternCharGroupHighlightStyleInfo );
						} );

						coloured_ranges.Set( current_group.Index );

						var eog = current_group.Groups["eog"];
						if( eog.Success )
						{
							ChangeEventHelper.BeginInvoke( ct, ( ) =>
							{
								td.Range( eog.Index, 1 ).Style( PatternCharGroupHighlightStyleInfo );
							} );

							coloured_ranges.Set( eog.Index );
						}
					}
				}

				var escapes = new List<Segment>( );

				foreach( Match group in matches.Where( m => { var g = m.Groups["character_group"]; return g.Success && g.Length > 2; } ) )
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

#endif

				var segments_to_uncolour = coloured_ranges.GetSegments( ct, false, top_index ).ToList( );


				//RtbUtilities.ClearProperties( ct, ChangeEventHelper, null, td, segments_to_uncolour );
				RtbUtilities.ApplyStyle( ct, ChangeEventHelper, null, td, segments_to_uncolour, PatternNormalStyleInfo );

				t2 = DateTime.Now;

				Debug.WriteLine( "### Uncolour: {0:F0}", ( t2 - t1 ).TotalMilliseconds );

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


		string BuildPattern( RegexOptions options )
		{
			bool ignore_pattern_whitespace = options.HasFlag( RegexOptions.IgnorePatternWhitespace );

			string pattern = $@"
					(?'inline_comment'\(\?\#.*?(\)|$)) |
                    (?'para'(?'left_para'\()|(?'right_para'\))) |
                    (?'character_group'\[(\\.|.)*?(?'eog'\])) |
                    {( ignore_pattern_whitespace ? @"(?'eol_comment'\#[^\n]*) |" : "" )}
                    (?'other'(\\.|[^\(\)\[\]{( ignore_pattern_whitespace ? "#" : "" )}])+)
                    ";

			return pattern;
		}


		Regex GetColouringRegex( RegexOptions options )
		{
			bool ignore_pattern_whitespace = options.HasFlag( RegexOptions.IgnorePatternWhitespace );

			if( ignore_pattern_whitespace )
			{
				return IgnorePatternWhitespaceRegex;
			}
			else
			{
				return NoIgnorePatternWhitespaceRegex;
			}
		}


		private void ColouriseComments( CancellationToken ct, TextData td, NaiveRanges colouredRanges, Rect clipRect, int topIndex, int bottomIndex, Match[] matches )
		{
			var ranges = new NaiveRanges( bottomIndex - topIndex + 1 );

			var groups1 =
				matches
					.Select( m => m.Groups["inline_comment"] )
					.Where( g => g.Success );

			var groups2 =
				matches
					.Select( m => m.Groups["eol_comment"] )
					.Where( g => g.Success );

			foreach( Group g in groups1 )
			{
				ct.ThrowIfCancellationRequested( );

				if( g.Index > bottomIndex ) break;

				ranges.SafeSet( g.Index - topIndex, g.Length );
			}

			foreach( Group g in groups2 )
			{
				ct.ThrowIfCancellationRequested( );

				if( g.Index > bottomIndex ) break;

				ranges.SafeSet( g.Index - topIndex, g.Length );
			}

			List<Segment> segments = ranges.GetSegments( ct, true, topIndex ).ToList( );

			RtbUtilities.ApplyStyle( ct, ChangeEventHelper, null, td, segments, PatternCommentStyleInfo );

			colouredRanges.Set( ranges );
		}


		private void ColouriseEscapes( CancellationToken ct, TextData td, NaiveRanges colouredRanges, Rect clipRect, int topIndex, int bottomIndex, Match[] matches )
		{
			var ranges = new NaiveRanges( bottomIndex - topIndex + 1 );

			var groups1 = matches
				.Select( m => m.Groups["character_group"] )
				.Where( g => g.Success );

			var groups2 = matches
				.Select( m => m.Groups["other"] )
				.Where( g => g.Success );

			foreach( Group g in groups1 )
			{
				ct.ThrowIfCancellationRequested( );

				if( g.Index > bottomIndex ) break;

				foreach( Match m in EscapeRegex.Matches( g.Value ) )
				{
					ct.ThrowIfCancellationRequested( );

					ranges.SafeSet( g.Index - topIndex + m.Index, m.Length );
				}
			}

			foreach( Group g in groups2 )
			{
				ct.ThrowIfCancellationRequested( );

				if( g.Index > bottomIndex ) break;

				foreach( Match m in EscapeRegex.Matches( g.Value ) )
				{
					ct.ThrowIfCancellationRequested( );

					ranges.SafeSet( g.Index - topIndex + m.Index, m.Length );
				}
			}

			List<Segment> segments = ranges.GetSegments( ct, true, topIndex ).ToList( );

			RtbUtilities.ApplyStyle( ct, ChangeEventHelper, null, td, segments, PatternEscapeStyleInfo );

			colouredRanges.Set( ranges );
		}


		private void ColouriseNamedGroups( CancellationToken ct, TextData td, NaiveRanges colouredRanges, Rect clipRect, int topIndex, int bottomIndex, Match[] matches )
		{
			var ranges = new NaiveRanges( bottomIndex - topIndex + 1 );

			var left_parentheses = matches
				.Select( m => m.Groups["left_para"] )
				.Where( g => g.Success );

			foreach( var g in left_parentheses )
			{
				ct.ThrowIfCancellationRequested( );

				if( g.Index > bottomIndex ) break;

				// (balancing groups covered too)

				var m = NamedGroupsRegex.Match( td.Text, g.Index );
				if( m.Success )
				{
					var gn = m.Groups["name"];
					Debug.Assert( gn.Success );

					ranges.SafeSet( gn.Index - topIndex, gn.Length );
				}
			}

			List<Segment> segments = ranges.GetSegments( ct, true, topIndex ).ToList( );

			RtbUtilities.ApplyStyle( ct, ChangeEventHelper, null, td, segments, PatternGroupNameStyleInfo );

			colouredRanges.Set( ranges );
		}



		static void GetRecolorEscapes( List<Segment> list, TextData td, int start, int len, CancellationToken ct )
		{
			if( len == 0 ) return;

			string s = td.Text.Substring( start, len );

			const string pattern = @"(?>\\[0-7]{2,3} | \\x[0-9A-F]{2} | \\c[A-Z] | \\u[0-9A-F]{4} | \\p\{[A-Z]+\} | \\k<[A-Z]+> | \\.)";

			var ms = Regex.Matches( s, pattern, RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace );

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
