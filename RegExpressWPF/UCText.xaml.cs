using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using RegExpressWPF.Code;


namespace RegExpressWPF
{
    /// <summary>
    /// Interaction logic for UCText.xaml
    /// </summary>
    public partial class UCText : UserControl
    {
        readonly RtbAdorner RtbAdorner;

        readonly TaskHelper RecolouringTask = new TaskHelper( );
        readonly TaskHelper UnderliningTask = new TaskHelper( );

        readonly ChangeEventHelper ChangeEventHelper;
        readonly UndoRedoHelper UndoRedoHelper;

        bool AlreadyLoaded = false;
        IReadOnlyList<Match> LastMatches; // null if no data, or recolouring process is not finished
        bool LastShowCaptures;
        string LastEol;
        IReadOnlyList<Segment> LastUnderlines;

        readonly StyleInfo[] HighlightStyleInfos;

        readonly TextDecorationCollection UnderlineTextDecorations;
        readonly LengthConverter LengthConverter = new LengthConverter( );


        public event EventHandler TextChanged;
        public event EventHandler SelectionChanged;
        public event EventHandler LocalUnderliningFinished;


        public UCText( )
        {
            InitializeComponent( );

            RtbAdorner = new RtbAdorner( rtb );

            ChangeEventHelper = new ChangeEventHelper( this.rtb );
            UndoRedoHelper = new UndoRedoHelper( this.rtb );

            HighlightStyleInfos = new[]
            {
                new StyleInfo( "MatchHighlight_0" ),
                new StyleInfo( "MatchHighlight_1" ),
                new StyleInfo( "MatchHighlight_2" )
            };

            var text_decoration = (TextDecoration)App.Current.Resources["Underline"];
            UnderlineTextDecorations = new TextDecorationCollection( );
            UnderlineTextDecorations.Add( text_decoration );
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


        public void SetMatches( IReadOnlyList<Match> matches, bool showCaptures, string eol )
        {
            if( matches == null ) throw new ArgumentNullException( "matches" );

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
            LastUnderlines = null;

            RestartRecolouring( matches, showCaptures, eol );
        }


        public IReadOnlyList<Segment> GetUnderliningInfo( )
        {
            lock( this )
            {
                if( LastMatches == null ) // no data or processes not finished
                {
                    return Enumerable.Empty<Segment>( ).ToList( ).AsReadOnly( );
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


        public void SetUnderlinedCaptures( IReadOnlyList<Segment> segments )
        {
            lock( this )
            {
                if( LastMatches != null ) RestartExternalUnderlining( segments, LastEol );
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
            adorner_layer.Add( RtbAdorner );

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
            LastUnderlines = null;

            TextChanged?.Invoke( this, null );
        }


        private void Rtb_GotFocus( object sender, RoutedEventArgs e )
        {
            lock( this )
            {
                if( LastMatches != null ) RestartLocalUnderlining( LastMatches, LastShowCaptures, LastEol );
            }
        }


        private void Rtb_LostFocus( object sender, RoutedEventArgs e )
        {
            lock( this )
            {
                if( LastMatches != null ) RestartLocalUnderlining( Enumerable.Empty<Match>( ).ToList( ).AsReadOnly( ), LastShowCaptures, LastEol );
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


        void RecolourTaskProc( CancellationToken ct, IReadOnlyList<Match> matches, bool showCaptures, string eol )
        {
            try
            {
                if( ct.WaitHandle.WaitOne( 111 ) ) return;
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


                int show_pb_time = unchecked(Environment.TickCount + 333); // (ignore overflow)


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
                throw;
            }
        }


        void RestartLocalUnderlining( IReadOnlyList<Match> matches, bool showCaptures, string eol )
        {
            UnderliningTask.RestartAfter( RecolouringTask, ct => LocalUnderlineTaskProc( ct, matches, showCaptures, eol ) );
        }


        void RestartExternalUnderlining( IReadOnlyList<Segment> segments, string eol )
        {
            UnderliningTask.RestartAfter( RecolouringTask, ct => ExternalUnderlineTaskProc( ct, segments, eol ) );
        }


        void LocalUnderlineTaskProc( CancellationToken ct, IReadOnlyList<Match> matches, bool showCaptures, string eol )
        {
            try
            {
                if( ct.WaitHandle.WaitOne( 111 ) ) return;
                ct.ThrowIfCancellationRequested( );

                Debug.WriteLine( $"START LOCAL UNDERLINING" );
                DateTime start_time = DateTime.UtcNow;

                TextData td = null;

                ChangeEventHelper.Invoke( ct, ( ) =>
                {
                    td = rtb.GetTextData( eol );
                } );

                List<Segment> segments_to_underline = GetUnderliningInfo( ct, td, matches, showCaptures ).ToList( );

                if( LastUnderlines == null )
                {
                    RtbUtilities.ApplyProperty( ct, ChangeEventHelper, td, segments_to_underline, Inline.TextDecorationsProperty, UnderlineTextDecorations );

                    var underlined_ranges = new NaiveRanges( td.Text.Length );
                    underlined_ranges.Set( segments_to_underline );

                    var segments_to_deunderline = underlined_ranges.GetSegments( ct, false ).ToList( );

                    ct.ThrowIfCancellationRequested( );

                    RtbUtilities.ApplyProperty( ct, ChangeEventHelper, td, segments_to_deunderline, Inline.TextDecorationsProperty, null );

                    ChangeEventHelper.BeginInvoke( ct, ( ) =>
                    {
                        LocalUnderliningFinished?.Invoke( this, null );
                    } );
                }
                else
                {
                    var old_ranges = new NaiveRanges( td.Text.Length );
                    old_ranges.Set( LastUnderlines );

                    var new_ranges = new NaiveRanges( td.Text.Length );
                    new_ranges.Set( segments_to_underline );

                    var ranges_to_deunderline = old_ranges.MaterialNonimplication( new_ranges ); // (1x0=>1)
                    var ranges_to_underline = old_ranges.ConverseNonimplication( new_ranges ); // (0x1=>1)

                    LastUnderlines = null; // (stay null if cancelled)

                    RtbUtilities.ApplyProperty( ct, ChangeEventHelper, td, ranges_to_deunderline.GetSegments( ct, true ).ToList( ), Inline.TextDecorationsProperty, null );

                    RtbUtilities.ApplyProperty( ct, ChangeEventHelper, td, ranges_to_underline.GetSegments( ct, true ).ToList( ), Inline.TextDecorationsProperty, UnderlineTextDecorations );
                }

                LastUnderlines = segments_to_underline;

                Debug.WriteLine( $"TEXT UNDERLINED: {( DateTime.UtcNow - start_time ).TotalMilliseconds:#,##0}" );
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


        void ExternalUnderlineTaskProc( CancellationToken ct, IReadOnlyList<Segment> segments, string eol )
        {
            try
            {
                if( ct.WaitHandle.WaitOne( 111 ) ) return;
                ct.ThrowIfCancellationRequested( );

                Debug.WriteLine( $"START EXTERNAL UNDERLINING" );
                DateTime start_time = DateTime.UtcNow;

                var segments_to_underline = new List<Segment>( );

                foreach( var segment in segments )
                {
                    if( ct.IsCancellationRequested ) break;

                    segments_to_underline.Add( segment );
                }

                TextData td = null;

                ChangeEventHelper.Invoke( ct, ( ) =>
                {
                    td = rtb.GetTextData( eol );
                } );

                if( LastUnderlines == null )
                {
                    var underlined_ranges = new NaiveRanges( td.Text.Length );
                    underlined_ranges.Set( segments_to_underline );

                    RtbUtilities.ApplyProperty( ct, ChangeEventHelper, td, segments_to_underline, Inline.TextDecorationsProperty, UnderlineTextDecorations );

                    var segments_to_deunderline = underlined_ranges.GetSegments( ct, false ).ToList( );

                    ct.ThrowIfCancellationRequested( );

                    RtbUtilities.ApplyProperty( ct, ChangeEventHelper, td, segments_to_deunderline, Inline.TextDecorationsProperty, null );
                }
                else
                {
                    var old_ranges = new NaiveRanges( td.Text.Length );
                    old_ranges.Set( LastUnderlines );

                    var new_ranges = new NaiveRanges( td.Text.Length );
                    new_ranges.Set( segments_to_underline );

                    var ranges_to_deunderline = old_ranges.MaterialNonimplication( new_ranges ); // (1x0=>1)
                    var ranges_to_underline = old_ranges.ConverseNonimplication( new_ranges ); // (0x1=>1)

                    LastUnderlines = null; // (stay null if cancelled)

                    RtbUtilities.ApplyProperty( ct, ChangeEventHelper, td, ranges_to_deunderline.GetSegments( ct, true ).ToList( ), Inline.TextDecorationsProperty, null );

                    RtbUtilities.ApplyProperty( ct, ChangeEventHelper, td, ranges_to_underline.GetSegments( ct, true ).ToList( ), Inline.TextDecorationsProperty, UnderlineTextDecorations );
                }

                LastUnderlines = segments_to_underline;

                if( segments.Count > 0 )
                {
                    ChangeEventHelper.Invoke( ct, ( ) =>
                    {
                        var capture = segments.First( );

                        var r = td.Range( capture.Index, capture.Length );

                        switch( r.Start.Parent )
                        {
                        case FrameworkContentElement fce:
                            fce.BringIntoView( );
                            break;
                        case FrameworkElement fe:
                            fe.BringIntoView( );
                            break;
                        }
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
                throw;
            }
        }


        IReadOnlyList<Segment> GetUnderliningInfo( CancellationToken ct, TextData td, IReadOnlyList<Match> matches, bool showCaptures )
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
    }
}
