﻿using System;
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

		readonly Thread RecolouringThread;
		readonly AutoResetEvent RecolouringEvent = new AutoResetEvent( false );

		readonly Thread HighlightingThread;
		readonly AutoResetEvent HighlightingEvent = new AutoResetEvent( false );

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

		TextRange LeftHighlightedParantesis = null;
		TextRange RightHighlightedParantesis = null;
		TextRange LeftHighlightedBracket = null;
		TextRange RightHighlightedBracket = null;

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

			RecolouringThread = new Thread( RecolouringThreadProc )
			{
				IsBackground = true,
				Priority = ThreadPriority.BelowNormal,
			};
			RecolouringThread.Start( );

			HighlightingThread = new Thread( HighlightingThreadProc )
			{
				IsBackground = true,
				Priority = ThreadPriority.BelowNormal,
			};
			HighlightingThread.Start( );
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


		public void SetRegexOptions( RegexOptions regexOptions, string eol )
		{
			StopAll( );

			lock( this )
			{
				mRegexOptions = regexOptions;
				mEol = eol;
			}

			if( IsLoaded )
			{
				RecolouringEvent.Set( );
				HighlightingEvent.Set( );
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
			RecolouringEvent.Set( );
			HighlightingEvent.Set( );

			TextChanged?.Invoke( this, null );
		}


		private void rtb_SelectionChanged( object sender, RoutedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;
			if( !rtb.IsFocused ) return;

			UndoRedoHelper.HandleSelectionChanged( );
			HighlightingEvent.Set( );
		}


		private void rtb_ScrollChanged( object sender, ScrollChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			RecolouringEvent.Set( );
			HighlightingEvent.Set( );
		}


		private void rtb_SizeChanged( object sender, SizeChangedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			RecolouringEvent.Set( );
			HighlightingEvent.Set( );
		}


		private void rtb_GotFocus( object sender, RoutedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;

			HighlightingEvent.Set( );

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

			HighlightingEvent.Set( );
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


#if false
		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void RecolouringTaskProc( CancellationToken ct, bool is_focused, RegexOptions regexOptions, string eol )
		{
			try
			{
				int timeout = Math.Max( rtb.LastGetTextDataDuration, 222 );
				if( ct.WaitHandle.WaitOne( timeout ) ) return;
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
				if( td.Text.Length == 0 ) return;

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


				ColouriseComments( ct, td, coloured_ranges, clip_rect, top_index, bottom_index, matches );
				ColouriseEscapes( ct, td, coloured_ranges, clip_rect, top_index, bottom_index, matches );
				ColouriseNamedGroups( ct, td, coloured_ranges, clip_rect, top_index, bottom_index, matches );

				ChangeEventHelper.Invoke( ct,
					( ) =>
					{
						// ensure the highlighted items are not lost
						TryMark( coloured_ranges, top_index, td, LeftHighlightedParantesis?.Start );
						ct.ThrowIfCancellationRequested( );
						TryMark( coloured_ranges, top_index, td, RightHighlightedParantesis?.Start );
						ct.ThrowIfCancellationRequested( );
						TryMark( coloured_ranges, top_index, td, LeftHighlightedBracket?.Start );
						ct.ThrowIfCancellationRequested( );
						TryMark( coloured_ranges, top_index, td, RightHighlightedBracket?.Start );
					} );

				int center_index = ( top_index + bottom_index ) / 2;

				var segments_to_uncolour = coloured_ranges
					.GetSegments( ct, false, top_index )
					.OrderBy( s => Math.Abs( center_index - ( s.Index + s.Length / 2 ) ) )
					.ToList( );

				//RtbUtilities.ClearProperties( ct, ChangeEventHelper, null, td, segments_to_uncolour );
				RtbUtilities.ApplyStyle( ct, ChangeEventHelper, null, td, segments_to_uncolour, PatternNormalStyleInfo );
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
#endif


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void RecolouringThreadProc( )
		{
			try
			{
				for(; ; )
				{
					// TODO: consider things related to termination

					RecolouringEvent.WaitOne( Timeout.Infinite );

					for(; ; )
					{
						int timeout = 222;
						while( RecolouringEvent.WaitOne( timeout ) ) { timeout = 444; }

						RegexOptions regex_options;
						string eol;

						lock( this )
						{
							regex_options = mRegexOptions;
							eol = mEol;
						}

						bool MustRestart( )
						{
							return RecolouringEvent.WaitOne( 0 );
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

						if( MustRestart( ) ) continue;
						if( td == null ) break;
						if( td.Text.Length == 0 ) break;

						Debug.Assert( top_index >= 0 );
						Debug.Assert( bottom_index >= top_index );
						Debug.Assert( bottom_index < td.Pointers.Count );

						var regex = GetColouringRegex( regex_options );
						var coloured_ranges = new NaiveRanges( bottom_index - top_index + 1 );

						var matches = regex.Matches( td.Text )
							.Cast<Match>( )
							.ToArray( );

						if( MustRestart( ) ) continue;

						if( !ColouriseComments( td, coloured_ranges, clip_rect, top_index, bottom_index, matches ) ) continue;
						if( !ColouriseEscapes( td, coloured_ranges, clip_rect, top_index, bottom_index, matches ) ) continue;
						if( !ColouriseNamedGroups( td, coloured_ranges, clip_rect, top_index, bottom_index, matches ) ) continue;

						bool must_restart = false;

						ChangeEventHelper.Invoke( CancellationToken.None,
							( ) =>
							{
								// ensure the highlighted items are not lost
								TryMark( coloured_ranges, top_index, td, LeftHighlightedParantesis?.Start );
								if( MustRestart( ) ) { must_restart = true; return; }
								TryMark( coloured_ranges, top_index, td, RightHighlightedParantesis?.Start );
								if( MustRestart( ) ) { must_restart = true; return; }
								TryMark( coloured_ranges, top_index, td, LeftHighlightedBracket?.Start );
								if( MustRestart( ) ) { must_restart = true; return; }
								TryMark( coloured_ranges, top_index, td, RightHighlightedBracket?.Start );
							} );

						if( must_restart ) continue;
						if( MustRestart( ) ) continue;

						int center_index = ( top_index + bottom_index ) / 2;

						var segments_to_uncolour = coloured_ranges
							.GetSegments( CancellationToken.None, false, top_index ) //... make it cancellable
							.OrderBy( s => Math.Abs( center_index - ( s.Index + s.Length / 2 ) ) )
							.ToList( );

						//... make it cancellable
						//RtbUtilities.ClearProperties( ct, ChangeEventHelper, null, td, segments_to_uncolour );
						RtbUtilities.ApplyStyle( CancellationToken.None, ChangeEventHelper, null, td, segments_to_uncolour, PatternNormalStyleInfo );

						break;
					}
				}
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
		void HighlightingThreadProc( )
		{
			try
			{
				for(; ; )
				{
					// TODO: consider things related to termination

					HighlightingEvent.WaitOne( Timeout.Infinite );

					for(; ; )
					{
						int timeout = 111;
						while( HighlightingEvent.WaitOne( timeout ) ) { timeout = 444; }

						RegexOptions regex_options;
						string eol;

						lock( this )
						{
							regex_options = mRegexOptions;
							eol = mEol;
						}

						bool MustRestart( )
						{
							return HighlightingEvent.WaitOne( 0 );
						}

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

							is_focused = rtb.IsFocused;
						} );

						if( MustRestart( ) ) continue;
						if( td == null ) break;
						if( td.Text.Length == 0 ) break;

						Debug.Assert( top_index >= 0 );
						Debug.Assert( bottom_index >= top_index );
						Debug.Assert( bottom_index < td.Pointers.Count );

						var regex = GetColouringRegex( regex_options );

						var matches = regex
							.Matches( td.Text )
							.Cast<Match>( )
							.ToArray( );

						if( MustRestart( ) ) continue;

						bool must_restart = false;

						int left_para_index = -1;
						int right_para_index = -1;
						int left_bracket_index = -1;
						int right_bracket_index = -1;

						if( is_focused )
						{
							var parentheses = matches.Where( m => m.Groups["para"].Success ).ToArray( );
							if( MustRestart( ) ) continue;
							var parentheses_at_left = parentheses.Where( m => m.Index < td.SelectionStart ).ToArray( );
							if( MustRestart( ) ) continue;
							var parentheses_at_right = parentheses.Where( m => m.Index >= td.SelectionStart ).ToArray( );
							if( MustRestart( ) ) continue;

							if( parentheses_at_left.Any( ) )
							{
								int n = 0;
								int found_i = -1;
								for( int i = parentheses_at_left.Length - 1; i >= 0; --i )
								{
									if( MustRestart( ) ) { must_restart = true; break; }

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

							if( must_restart || MustRestart( ) ) continue;

							if( parentheses_at_right.Any( ) )
							{
								int n = 0;
								int found_i = -1;
								for( int i = 0; i < parentheses_at_right.Length; ++i )
								{
									if( MustRestart( ) ) { must_restart = true; break; }

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

							if( must_restart || MustRestart( ) ) continue;

							var current_group = matches.Where( m => m.Groups["character_group"].Success && m.Index <= td.SelectionStart && m.Index + m.Length > td.SelectionStart ).FirstOrDefault( );

							if( MustRestart( ) ) continue;

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

						if( MustRestart( ) ) continue;

						ChangeEventHelper.Invoke( CancellationToken.None, ( ) =>
						{
							TryHighlight( ref LeftHighlightedParantesis, td, left_para_index, PatternParaHighlightStyleInfo );
							if( MustRestart( ) ) { must_restart = true; return; }
							TryHighlight( ref RightHighlightedParantesis, td, right_para_index, PatternParaHighlightStyleInfo );
							if( MustRestart( ) ) { must_restart = true; return; }
							TryHighlight( ref LeftHighlightedBracket, td, left_bracket_index, PatternCharGroupHighlightStyleInfo );
							if( MustRestart( ) ) { must_restart = true; return; }
							TryHighlight( ref RightHighlightedBracket, td, right_bracket_index, PatternCharGroupHighlightStyleInfo );
						} );

						if( must_restart ) continue;

						break;
					}
				}
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


		void TryMark( NaiveRanges ranges, int topIndex, TextData td, TextPointer tp )
		{
			if( tp != null )
			{
				int i = RtbUtilities.Find( td.Pointers, tp );
				if( i >= 0 )
				{
					ranges.SafeSet( i - topIndex );
				}
			}
		}


		void TryHighlight( ref TextRange tr, TextData td, int index, StyleInfo styleInfo )
		{
			// TODO: avoid flickering

			if( tr != null )
			{
				if( tr.Start.IsInSameDocument( rtb.Document.ContentStart ) )
				{
					tr.Style( PatternNormalStyleInfo );
				}
			}

			tr = null;

			if( index >= 0 ) tr = td.Range( index, 1 );

			if( tr != null ) tr.Style( styleInfo );
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


		private bool ColouriseComments( TextData td, NaiveRanges colouredRanges, Rect clipRect, int topIndex, int bottomIndex, Match[] matches )
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
				if( RecolouringEvent.WaitOne( 0 ) ) return false;

				if( g.Index > bottomIndex ) break;

				ranges.SafeSet( g.Index - topIndex, g.Length );
			}

			foreach( Group g in groups2 )
			{
				if( RecolouringEvent.WaitOne( 0 ) ) return false;

				if( g.Index > bottomIndex ) break;

				ranges.SafeSet( g.Index - topIndex, g.Length );
			}

			int center_index = ( topIndex + bottomIndex ) / 2;

			List<Segment> segments = ranges
				.GetSegments( CancellationToken.None, true, topIndex ) //... make it cancellable
				.OrderBy( s => Math.Abs( center_index - ( s.Index + s.Length / 2 ) ) )
				.ToList( );

			//...
			RtbUtilities.ApplyStyle( CancellationToken.None, ChangeEventHelper, null, td, segments, PatternCommentStyleInfo );

			colouredRanges.Set( ranges );

			return true;
		}


		private bool ColouriseEscapes( TextData td, NaiveRanges colouredRanges, Rect clipRect, int topIndex, int bottomIndex, Match[] matches )
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
				if( RecolouringEvent.WaitOne( 0 ) ) return false;

				if( g.Index > bottomIndex ) break;

				foreach( Match m in EscapeRegex.Matches( g.Value ) )
				{
					if( RecolouringEvent.WaitOne( 0 ) ) return false;

					ranges.SafeSet( g.Index - topIndex + m.Index, m.Length );
				}
			}

			foreach( Group g in groups2 )
			{
				if( RecolouringEvent.WaitOne( 0 ) ) return false;

				if( g.Index > bottomIndex ) break;

				foreach( Match m in EscapeRegex.Matches( g.Value ) )
				{
					if( RecolouringEvent.WaitOne( 0 ) ) return false;

					ranges.SafeSet( g.Index - topIndex + m.Index, m.Length );
				}
			}

			int center_index = ( topIndex + bottomIndex ) / 2;

			List<Segment> segments = ranges
					.GetSegments( CancellationToken.None, true, topIndex ) //...
					.OrderBy( s => Math.Abs( center_index - ( s.Index + s.Length / 2 ) ) )
					.ToList( );

			//...
			RtbUtilities.ApplyStyle( CancellationToken.None, ChangeEventHelper, null, td, segments, PatternEscapeStyleInfo );

			colouredRanges.Set( ranges );

			return true;
		}


		private bool ColouriseNamedGroups( TextData td, NaiveRanges colouredRanges, Rect clipRect, int topIndex, int bottomIndex, Match[] matches )
		{
			var ranges = new NaiveRanges( bottomIndex - topIndex + 1 );

			var left_parentheses = matches
				.Select( m => m.Groups["left_para"] )
				.Where( g => g.Success );

			foreach( var g in left_parentheses )
			{
				if( RecolouringEvent.WaitOne( 0 ) ) return false;

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

			int center_index = ( topIndex + bottomIndex ) / 2;

			List<Segment> segments = ranges
				.GetSegments( CancellationToken.None, true, topIndex ) //...
				.OrderBy( s => Math.Abs( center_index - ( s.Index + s.Length / 2 ) ) )
				.ToList( );

			//...
			RtbUtilities.ApplyStyle( CancellationToken.None, ChangeEventHelper, null, td, segments, PatternGroupNameStyleInfo );

			colouredRanges.Set( ranges );

			return true;
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

					//...
					using( RecolouringEvent ) { }
					using( HighlightingEvent ) { }
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
