﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using RegExpressWPF.Code;


namespace RegExpressWPF
{
    /// <summary>
    /// Interaction logic for UCPattern.xaml
    /// </summary>
    public partial class UCPattern : UserControl
    {
        const char WHITESPACE_CHAR = '\u00B7';

        readonly TaskHelper RecolouringTask = new TaskHelper( );

        readonly ChangeEventHelper ChangeEventHelper;
        readonly UndoRedoHelper UndoRedoHelper;

        readonly StyleInfo PatternParaHighlightStyleInfo;
        readonly StyleInfo PatternEscapeStyleInfo;
        readonly StyleInfo PatternGroupHighlightStyleInfo;
        readonly StyleInfo CommentStyleInfo;

        readonly Brush NormalBackgroundBrush;
        readonly Brush WhitespaceBackgroundForRichTextBox;
        readonly Brush WhitespaceBackgroundForParagraphs;
        readonly Brush WhitespaceBackgroundForRuns;
        readonly Style NormalParagraphStyle;
        readonly Style LastEmptyParagraphStyle;

        RegexOptions mRegexOptions;
        bool mShowTrailingWhitespaces = true;


        public event EventHandler TextChanged;


        public UCPattern( )
        {
            InitializeComponent( );

            ChangeEventHelper = new ChangeEventHelper( this.rtb );
            UndoRedoHelper = new UndoRedoHelper( this.rtb );

            PatternParaHighlightStyleInfo = new StyleInfo( "PatternParaHighlight" );
            PatternEscapeStyleInfo = new StyleInfo( "PatternEscape" );
            PatternGroupHighlightStyleInfo = new StyleInfo( "PatternGroupHighlight" );
            CommentStyleInfo = new StyleInfo( "PatternComment" );

            NormalBackgroundBrush = (Brush)App.Current.Resources["NormalBackground"];
            WhitespaceBackgroundForRichTextBox = (Brush)App.Current.Resources["WhitespaceBackgroundForRichTextBox"];
            WhitespaceBackgroundForParagraphs = (Brush)App.Current.Resources["WhitespaceBackgroundForParagraphs"];
            WhitespaceBackgroundForRuns = (Brush)App.Current.Resources["WhitespaceBackgroundForRuns"];
            NormalParagraphStyle = (Style)rtb.Style.Resources["NormalParagraphStyle"];
            LastEmptyParagraphStyle = (Style)rtb.Style.Resources["LastEmptyParagraphStyle"];
        }


        public string GetText( string eol )
        {
            var td = rtb.GetTextData( eol );

            return td.Text;
        }


        public string Text
        {
            set
            {
                RtbUtilities.SetText( rtb, value );

                UndoRedoHelper.Init( );
            }
        }


        public void SetRegexOptions( RegexOptions regexOptions )
        {
            StopAll( );
            mRegexOptions = regexOptions;
            if( IsLoaded ) RestartRecolouring( );
        }


        public bool ShowTrailingWhitespaces
        {
            set
            {
                mShowTrailingWhitespaces = value; // (atomic)

                if( IsLoaded )
                {
                    ApplyShowWhitespaces( CancellationToken.None, null );
                }
            }
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

            TextChanged?.Invoke( this, null );

            RestartRecolouring( );
        }


        private void Rtb_GotFocus( object sender, RoutedEventArgs e )
        {
            if( !IsLoaded ) return;
            if( ChangeEventHelper.IsInChange ) return;

            RestartRecolouring( );
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


        private void Rtb_Copying( object sender, DataObjectCopyingEventArgs e )
        {
        }


        private void Rtb_SettingData( object sender, DataObjectSettingDataEventArgs e )
        {
            if( e.Format == DataFormats.Text || e.Format == DataFormats.UnicodeText )
            {
                string old_text = rtb.Selection.Text;
                string new_text = old_text.Replace( WHITESPACE_CHAR, ' ' );

                e.DataObject.SetData( e.Format, new_text, true );

                e.Handled = true; //?
                e.CancelCommand( );
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


        void RecolourTaskProc( CancellationToken ct, bool is_focused, RegexOptions regexOptions )
        {
            try
            {
                if( ct.WaitHandle.WaitOne( 333 ) ) return;
                ct.ThrowIfCancellationRequested( );

                TextData td = null;

                ChangeEventHelper.Invoke( ct, ( ) =>
                {
                    td = rtb.GetTextData( "\n" );
                } );

                ct.ThrowIfCancellationRequested( );

                var coloured_ranges = new NaiveRanges( td.Text.Length );

                bool ignore_pattern_whitespace = ( regexOptions & RegexOptions.IgnorePatternWhitespace ) != 0;

                string pattern = $@"
                    (?'para'\(|\)) |
                    (?'group'\[(\\.|.)*?(?'eog'\])) |
                    {( ignore_pattern_whitespace ? @"(?'comment'\#[^\r\n]*) |" : "" )}
                    (?'other'(\\.|[^\(\)\[\]{( ignore_pattern_whitespace ? "#" : "" )}])+)
                    ";

                var matches = Regex.Matches( td.Text, pattern, RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline )
                    .Cast<Match>( )
                    .ToArray( );

                ct.ThrowIfCancellationRequested( );

                if( ignore_pattern_whitespace )
                {
                    List<Segment> comment_segments =
                        matches
                            .Select( m => m.Groups["comment"] )
                            .Where( g => g.Success )
                            .Select( g => new Segment( g.Index, g.Length ) )
                            .ToList( );

                    RtbUtilities.ApplyStyle( ct, ChangeEventHelper, null, td, comment_segments, CommentStyleInfo );

                    coloured_ranges.Set( comment_segments );
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

                // decide about whitespaces
                ApplyShowWhitespaces( ct, td );
            }
            catch( OperationCanceledException ) // also 'TaskCanceledException'
            {
                // ignore
            }
            catch( Exception exc )
            {
                throw;
            }
        }


        void GetRecolorEscapes( List<Segment> list, TextData td, int start, int len, CancellationToken ct )
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


        bool ShouldShowWhitespaces( string text )
        {
            return Regex.IsMatch( text, @"(^|\r|\n)( |\t)|( |\t)(\r|\n|$)", RegexOptions.ExplicitCapture );
        }


        bool ShouldShowLastParagraphAsEmpty( string text )
        {
            return Regex.IsMatch( text, @"(\r|\n)$", RegexOptions.ExplicitCapture );
        }


        void ApplyShowWhitespaces( CancellationToken ct, TextData td0 )
        {
            /*
            Brush brush_rtb;
            Brush brush_para;
            Brush brush_runs;
            Style style_last_para;

            if( mShowTrailingWhitespaces && ( mRegexOptions & RegexOptions.IgnorePatternWhitespace ) == 0 )
            {
                TextData td = td0 ?? rtb.GetTextData( "\n" );

                if( ShouldShowWhitespaces( td.Text ) )
                {
                    brush_rtb = WhitespaceBackgroundForRichTextBox;
                    brush_para = WhitespaceBackgroundForParagraphs;
                    brush_runs = WhitespaceBackgroundForRuns;
                }
                else
                {
                    brush_rtb = NormalBackgroundBrush;
                    brush_para = NormalBackgroundBrush;
                    brush_runs = NormalBackgroundBrush;
                }

                if( ShouldShowLastParagraphAsEmpty( td.Text ) )
                {
                    style_last_para = LastEmptyParagraphStyle;
                }
                else
                {
                    style_last_para = NormalParagraphStyle;
                }
            }
            else
            {
                brush_rtb = NormalBackgroundBrush;
                brush_para = NormalBackgroundBrush;
                brush_runs = NormalBackgroundBrush;
                style_last_para = NormalParagraphStyle;
            }

            ChangeEventHelper.BeginInvoke( ct, ( ) =>
            {
                if( rtb.Resources["DynamicBackgroundForRichTextBox"] != brush_rtb ) rtb.Resources["DynamicBackgroundForRichTextBox"] = brush_rtb;
                if( rtb.Resources["DynamicBackgroundForParagraphs"] != brush_para ) rtb.Resources["DynamicBackgroundForParagraphs"] = brush_para;
                if( rtb.Resources["DynamicBackgroundForRuns"] != brush_runs ) rtb.Resources["DynamicBackgroundForRuns"] = brush_runs;

                Paragraph last_para_helper = null;

                RtbUtilities.ForEachParagraphBackward( ct, rtb.Document.Blocks, ref last_para_helper, ( p, l ) =>
                {
                    if( l )
                    {
                        if( p.Style != style_last_para ) p.Style = style_last_para;
                    }
                    else
                    {
                        if( p.Style != NormalParagraphStyle ) p.Style = NormalParagraphStyle;
                    }
                } );
            } );
            */

            TextData td = td0 ?? rtb.GetTextData( "\n" );

            ChangeEventHelper.Invoke( ct, ( ) =>
            {
                ShowWhitespaces( ct, td, rtb.Document.Blocks );//..........
            } );

        }


        void ShowWhitespaces( CancellationToken ct, TextData td, BlockCollection blocks )
        {
            bool changed = false;
            List<Run> runs_to_adjust = new List<Run>( );

            try
            {
                foreach( var block in blocks )
                {
                    if( ct.IsCancellationRequested ) break;

                    switch( block )
                    {
                    case Paragraph para:

                        bool last_found = false;

                        for( var inline = para.Inlines.LastInline; inline != null; inline = inline.PreviousInline )
                        {
                            if( ct.IsCancellationRequested ) break;

                            switch( inline )
                            {
                            case Run run:
                                if( run.Text.Length != 0 )
                                {
                                    if( !last_found )
                                    {
                                        if( run.Text.EndsWith( " " ) || run.Text.Contains( WHITESPACE_CHAR ) )
                                        {
                                            runs_to_adjust.Add( run );
                                        }

                                        last_found = true;
                                    }
                                }
                                break;
                            case LineBreak lb:
                                last_found = false;
                                break;
                            }
                        }

                        break;
                    case Section section:
                        ShowWhitespaces( ct, td, section.Blocks );
                        break;
                    default:
                        Debug.Fail( "NOT SUPPORTED: " + block.GetType( ) );
                        break;
                    }
                }

                foreach( var run in runs_to_adjust )
                {
                    var old_text = run.Text;
                    var new_text = old_text.Replace( WHITESPACE_CHAR, ' ' );
                    new_text = Regex.Replace( new_text, @"(\s+)$", ( m ) => new string( WHITESPACE_CHAR, m.Groups[1].Length ) );

                    if( new_text != old_text )
                    {
                        run.Text = new_text;
                        changed = true;
                    }
                }

                if( changed )
                {
                    // have to rescan if text changed; 
                    // example: the line was "     ", then a letter is typed in the middle;
                    // otherwise the cursor goes to the start of line for unknown reasons
                    TextData td2 = rtb.GetTextData( td.Eol );

                    rtb.Selection.Select( td2.Pointers[td.SelectionStart], td2.Pointers[td.SelectionEnd] );
                }
            }
            catch( Exception exc )
            {
                throw;
            }
        }
    }
}
