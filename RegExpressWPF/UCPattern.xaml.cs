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

		int LeftHighlightedParantesis = -1;
		int RightHighlightedParantesis = -1;
		int LeftHighlightedBracket = -1;
		int RightHighlightedBracket = -1;

		RegexEngine mRegexEngine;
		IReadOnlyCollection<RegexOptionInfo> mRegexOptions;
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


		public void SetRegexOptions( RegexEngine engine, IReadOnlyCollection<RegexOptionInfo> regexOptions, string eol )
		{
			StopAll( );

			lock( this )
			{
				mRegexEngine = engine;
				mRegexOptions = regexOptions;
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
			RegexEngine regex_engine;
			IReadOnlyCollection<RegexOptionInfo> regex_options;
			string eol;

			lock( this )
			{
				regex_engine = mRegexEngine;
				regex_options = mRegexOptions;
				eol = mEol;
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

			var regex = GetColouringRegex( regex_engine, regex_options );
			var coloured_ranges = new NaiveRanges( bottom_index - top_index + 1 );

			var matches = regex
				.Matches( td.Text )
				.Cast<Match>( )
				.ToArray( );

			if( cnc.IsCancellationRequested ) return;

			ColouriseComments( cnc, td, coloured_ranges, clip_rect, top_index, bottom_index, matches );
			if( cnc.IsCancellationRequested ) return;

			ColouriseEscapes( cnc, td, coloured_ranges, clip_rect, top_index, bottom_index, matches );
			if( cnc.IsCancellationRequested ) return;

			ColouriseNamedGroups( cnc, td, coloured_ranges, clip_rect, top_index, bottom_index, matches );
			if( cnc.IsCancellationRequested ) return;

			lock( Locker )
			{
				ChangeEventHelper.Invoke( CancellationToken.None,
					( ) =>
					{
						// ensure the highlighted items are not lost
						TryMark( coloured_ranges, top_index, td, LeftHighlightedParantesis );
						if( cnc.IsCancellationRequested ) return;

						TryMark( coloured_ranges, top_index, td, RightHighlightedParantesis );
						if( cnc.IsCancellationRequested ) return;

						TryMark( coloured_ranges, top_index, td, LeftHighlightedBracket );
						if( cnc.IsCancellationRequested ) return;

						TryMark( coloured_ranges, top_index, td, RightHighlightedBracket );
					} );

				if( cnc.IsCancellationRequested ) return;

				int center_index = ( top_index + bottom_index ) / 2;

				var segments_to_uncolour = coloured_ranges
					.GetSegments( cnc, false, top_index )
					.OrderBy( s => Math.Abs( center_index - ( s.Index + s.Length / 2 ) ) )
					.ToList( );

				if( cnc.IsCancellationRequested ) return;

				//RtbUtilities.ClearProperties( ct, ChangeEventHelper, null, td, segments_to_uncolour );
				RtbUtilities.ApplyStyle( cnc, ChangeEventHelper, null, td, segments_to_uncolour, PatternNormalStyleInfo );
			}
		}


		void HighlightingThreadProc( ICancellable cnc )
		{
			RegexEngine regex_engine;
			IReadOnlyCollection<RegexOptionInfo> regex_options;
			string eol;

			lock( this )
			{
				regex_engine = mRegexEngine;
				regex_options = mRegexOptions;
				eol = mEol;
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

			var regex = GetColouringRegex( regex_engine, regex_options );

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
		}


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


		Regex GetColouringRegex( RegexEngine engine, IReadOnlyCollection<RegexOptionInfo> options )
		{
			//..................
			// TODO: implement
			return IgnorePatternWhitespaceRegex;

			/*
			bool ignore_pattern_whitespace = options.HasFlag( RegexOptions.IgnorePatternWhitespace );

			if( ignore_pattern_whitespace )
			{
				return IgnorePatternWhitespaceRegex;
			}
			else
			{
				return NoIgnorePatternWhitespaceRegex;
			}
			*/
		}


		private bool ColouriseComments( ICancellable reh, TextData td, NaiveRanges colouredRanges, Rect clipRect, int topIndex, int bottomIndex, Match[] matches )
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
				if( reh.IsCancellationRequested ) return false;

				if( g.Index > bottomIndex ) break;

				ranges.SafeSet( g.Index - topIndex, g.Length );
			}

			foreach( Group g in groups2 )
			{
				if( reh.IsCancellationRequested ) return false;

				if( g.Index > bottomIndex ) break;

				ranges.SafeSet( g.Index - topIndex, g.Length );
			}

			int center_index = ( topIndex + bottomIndex ) / 2;

			List<Segment> segments = ranges
				.GetSegments( reh, true, topIndex )
				.OrderBy( s => Math.Abs( center_index - ( s.Index + s.Length / 2 ) ) )
				.ToList( );

			if( reh.IsCancellationRequested ) return false;

			if( !RtbUtilities.ApplyStyle( reh, ChangeEventHelper, null, td, segments, PatternCommentStyleInfo ) )
				return false;

			colouredRanges.Set( ranges );

			return true;
		}


		private bool ColouriseEscapes( ICancellable reh, TextData td, NaiveRanges colouredRanges, Rect clipRect, int topIndex, int bottomIndex, Match[] matches )
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
				if( reh.IsCancellationRequested ) return false;

				if( g.Index > bottomIndex ) break;

				foreach( Match m in EscapeRegex.Matches( g.Value ) )
				{
					if( reh.IsCancellationRequested ) return false;

					ranges.SafeSet( g.Index - topIndex + m.Index, m.Length );
				}
			}

			foreach( Group g in groups2 )
			{
				if( reh.IsCancellationRequested ) return false;

				if( g.Index > bottomIndex ) break;

				foreach( Match m in EscapeRegex.Matches( g.Value ) )
				{
					if( reh.IsCancellationRequested ) return false;

					ranges.SafeSet( g.Index - topIndex + m.Index, m.Length );
				}
			}

			int center_index = ( topIndex + bottomIndex ) / 2;

			List<Segment> segments = ranges
					.GetSegments( reh, true, topIndex )
					.OrderBy( s => Math.Abs( center_index - ( s.Index + s.Length / 2 ) ) )
					.ToList( );

			if( reh.IsCancellationRequested ) return false;

			if( !RtbUtilities.ApplyStyle( reh, ChangeEventHelper, null, td, segments, PatternEscapeStyleInfo ) )
				return false;

			colouredRanges.Set( ranges );

			return true;
		}


		private bool ColouriseNamedGroups( ICancellable reh, TextData td, NaiveRanges colouredRanges, Rect clipRect, int topIndex, int bottomIndex, Match[] matches )
		{
			var ranges = new NaiveRanges( bottomIndex - topIndex + 1 );

			var left_parentheses = matches
				.Select( m => m.Groups["left_para"] )
				.Where( g => g.Success );

			foreach( var g in left_parentheses )
			{
				if( reh.IsCancellationRequested ) return false;

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
				.GetSegments( reh, true, topIndex )
				.OrderBy( s => Math.Abs( center_index - ( s.Index + s.Length / 2 ) ) )
				.ToList( );

			if( reh.IsCancellationRequested ) return false;

			if( !RtbUtilities.ApplyStyle( reh, ChangeEventHelper, null, td, segments, PatternGroupNameStyleInfo ) )
				return false;

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
