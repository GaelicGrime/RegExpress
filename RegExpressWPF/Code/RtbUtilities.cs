using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;


namespace RegExpressWPF.Code
{
    public class TextData
    {
        public string Text; // (lines are separated by EOL specified in the call of 'GetText',
        public string Eol;  //  which is also kept in 'Eol')
        public List<TextPointer> Pointers; // (maps string index to TextPointer)
        public int SelectionStart;
        public int SelectionEnd;
    }


    public static class RtbUtilities
    {

        public static void SetText( RichTextBox rtb, string text )
        {
            using( rtb.DeclareChangeBlock( ) )
            {
                rtb.Document.Blocks.Clear( );

                foreach( var s in Regex.Split( text ?? "", @"\r\n|\n\r|\r|\n" ) )
                {
                    rtb.Document.Blocks.Add( new Paragraph( new Run( s ) ) );
                }
            }
        }


        public static TextData GetTextData( RichTextBox rtb, string eol )
        {
            Debug.Assert( eol == "\r\n" || eol == "\n\r" || eol == "\r" || eol == "\n" );

            FlowDocument doc = rtb.Document;

            var td = new TextData { Eol = eol, Pointers = new List<TextPointer>( ) };

            StringBuilder sb = new StringBuilder( );

            Paragraph prevPara = null;
            ProcessBlocks( sb, td.Pointers, ref prevPara, doc.Blocks, eol );

            td.Pointers.Add( doc.ContentEnd );

            td.Text = sb.ToString( );

            UpdateSelection( rtb, td );

            return td;
        }


        public static void UpdateSelection( RichTextBox rtb, TextData td )
        {
            TextSelection selection = rtb.Selection;
            TextPointer selection_start = selection.Start;
            TextPointer selection_end = selection.End;

            td.SelectionStart = 0;
            td.SelectionEnd = 0;

            for( int i = 0; i < td.Pointers.Count; i++ )
            {
                TextPointer ptr = td.Pointers[i];
                if( ptr.CompareTo( selection_start ) >= 0 )
                {
                    break;
                }

                ++td.SelectionStart;
            }

            for( int i = 0; i < td.Pointers.Count; i++ )
            {
                TextPointer ptr = td.Pointers[i];
                if( ptr.CompareTo( selection_end ) >= 0 )
                {
                    break;
                }

                ++td.SelectionEnd;
            }
        }


        public static Segment Find( TextData td, TextPointer start, TextPointer end )
        {
            Debug.Assert( start.CompareTo( end ) <= 0 );

            if( start.CompareTo( end ) == 0 ) return new Segment( -1, -1 );

            int count = td.Pointers.Count;
            int i = 0;
            while( i < count && td.Pointers[i].CompareTo( start ) < 0 ) ++i;

            if( i >= count ) return new Segment( -1, -1 );

            var index = i;

            while( i < count && td.Pointers[i].CompareTo( end ) <= 0 ) ++i;

            //if( i >= count ) return new Segment(-1, -1);

            return new Segment( index, i - index );
        }


        public static void ForEachParagraphBackward( CancellationToken ct, BlockCollection blocks, ref Paragraph lastPara, Action<Paragraph, bool> action )
        {
            for( var block = blocks.LastBlock; block != null; block = block.PreviousBlock )
            {
                if( ct.IsCancellationRequested ) break;

                switch( block )
                {
                case Paragraph para:
                    bool is_last = lastPara == null;
                    lastPara = para;
                    action( para, is_last );
                    break;
                case Section section:
                    ForEachParagraphBackward( ct, section.Blocks, ref lastPara, action );
                    break;
                default:
                    Debug.Fail( "NOT SUPPORTED: " + block.GetType( ) );
                    break;
                }
            }
        }


        static void ProcessBlocks( StringBuilder sb, IList<TextPointer> pointers, ref Paragraph prevPara, IEnumerable<Block> blocks, string eol )
        {
            foreach( var block in blocks )
            {
                switch( block )
                {
                case Section section:
                    ProcessBlocks( sb, pointers, ref prevPara, section.Blocks, eol );
                    break;
                case Paragraph para:
                {
                    if( prevPara != null )
                    {
                        sb.Append( eol );
                        for( var i = 0; i < eol.Length; ++i ) pointers.Add( prevPara.ContentEnd );
                    }
                    ProcessInlines( sb, pointers, para.Inlines, eol );
                    prevPara = para;
                }
                break;
                default:
                    Debug.Assert( false );
                    break;
                }
            }
        }


        static void ProcessInlines( StringBuilder sb, IList<TextPointer> pointers, IEnumerable<Inline> inlines, string eol )
        {
            foreach( Inline inline in inlines )
            {
                switch( inline )
                {
                case Run run:
                    var start = run.ContentStart;

                    for( int i = 0; i < run.Text.Length; ++i )
                    {
                        var c = run.Text[i];
                        var p = start.GetPositionAtOffset( i );
                        int next_i;

                        switch( c )
                        {
                        case '\r':
                            sb.Append( eol );
                            for( int j = 0; j < eol.Length; ++j ) pointers.Add( p );
                            next_i = i + 1;
                            if( next_i < run.Text.Length && run.Text[next_i] == '\n' ) ++i; // skip
                            break;
                        case '\n':
                            sb.Append( eol );
                            for( int j = 0; j < eol.Length; ++j ) pointers.Add( p );
                            next_i = i + 1;
                            if( next_i < run.Text.Length && run.Text[next_i] == '\r' ) ++i; // skip
                            break;
                        default:
                            sb.Append( c );
                            pointers.Add( p );
                            break;
                        }
                    }
                    break;
                case Span span:
                    ProcessInlines( sb, pointers, span.Inlines, eol );
                    break;
                case LineBreak lb:
                    sb.Append( eol );
                    for( int j = 0; j < eol.Length; ++j ) pointers.Add( lb.ContentStart );
                    break;
                }
            }
        }


        public static TextRange Range( this TextData td, int start, int len )
        {
            var range = new TextRange( td.Pointers[start], td.Pointers[start + len] );

            return range;
        }


        public static TextRange Range( this TextData td, Segment segment )
        {
            return Range( td, segment.Index, segment.Length );
        }


        //


        public static TextRange Style( this TextRange range, StyleInfo styleInfo )
        {
            foreach( var style_info in styleInfo.Values )
            {
                range.ApplyPropertyValue( style_info.prop, style_info.val );
            }

            return range;
        }


        public static TextRange Style( this TextRange range, params StyleInfo[] styleInfos )
        {
            foreach( var styleInfo in styleInfos )
            {
                Style( range, styleInfo );
            }

            return range;
        }


        public static Inline Style( this Inline inline, StyleInfo styleInfo )
        {
            foreach( var style_info in styleInfo.Values )
            {
                inline.SetValue( style_info.prop, style_info.val );
            }

            return inline;
        }


        public static Inline Style( this Inline inline, params StyleInfo[] styleInfos )
        {
            foreach( var style_info in styleInfos )
            {
                Style( inline, style_info );
            }

            return inline;
        }


        const int SEGMENT_LENGTH = 100;


        public static void ApplyStyle( CancellationToken ct, ChangeEventHelper ceh, ProgressBar pb, TextData td, IList<(Segment segment, StyleInfo styleInfo)> segmentsAndStyles )
        {
            // split into smaller segments

            var segments = new List<(int index, int length, StyleInfo styleInfo)>( segmentsAndStyles.Count );

            foreach( var segment_and_style in segmentsAndStyles )
            {
                int j = segment_and_style.segment.Index;
                int rem = segment_and_style.segment.Length;

                do
                {
                    ct.ThrowIfCancellationRequested( );

                    int len = Math.Min( SEGMENT_LENGTH, rem );

                    segments.Add( (j, len, segment_and_style.styleInfo) );

                    j += len;
                    rem -= len;

                } while( rem > 0 );
            }


            int show_pb_time = unchecked(Environment.TickCount + 333); // (ignore overflow)
            int last_i = segments.Count;

            if( pb != null )
            {
                ceh.Invoke( ct, ( ) =>
                {
                    pb.Visibility = Visibility.Hidden;
                    pb.Maximum = last_i;
                } );
            }

            //var rnd = new Random( );
            //segments = segments.OrderBy( s => rnd.Next() ).ToList( ); // just for fun

            for( int i = 0; i < last_i; )
            {
                ct.ThrowIfCancellationRequested( );

                ceh.Invoke( ct, ( ) =>
                {
                    var now = Environment.TickCount;

                    if( pb != null )
                    {
                        if( now > show_pb_time )
                        {
                            pb.Value = i;
                            pb.Visibility = Visibility.Visible;
                        }
                    }

                    var end = now + 222;
                    do
                    {
                        if( ct.IsCancellationRequested ) return;

                        var segment = segments[i];
                        td.Range( segment.index, segment.length ).Style( segment.styleInfo );

                    } while( ++i < last_i && Environment.TickCount < end );
                } );
            }
        }


        public static void ApplyStyle( CancellationToken ct, ChangeEventHelper ceh, ProgressBar pb, TextData td, IList<Segment> segments0, StyleInfo styleInfo )
        {
            // split into smaller segments

            var segments = new List<(int index, int length)>( segments0.Count );

            foreach( var segment in segments0 )
            {
                int j = segment.Index;
                int rem = segment.Length;

                do
                {
                    ct.ThrowIfCancellationRequested( );

                    int len = Math.Min( SEGMENT_LENGTH, rem );

                    segments.Add( (j, len) );

                    j += len;
                    rem -= len;

                } while( rem > 0 );
            }


            int show_pb_time = unchecked(Environment.TickCount + 333); // (ignore overflow)
            int last_i = segments.Count;

            if( pb != null )
            {
                ceh.Invoke( ct, ( ) =>
                {
                    pb.Visibility = Visibility.Hidden;
                    pb.Maximum = last_i;
                } );
            }

            //var rnd = new Random( );
            //segments = segments.OrderBy( s => rnd.Next() ).ToList( ); // just for fun

            for( int i = 0; i < last_i; )
            {
                ct.ThrowIfCancellationRequested( );

                ceh.Invoke( ct, ( ) =>
                {
                    var now = Environment.TickCount;

                    if( pb != null )
                    {
                        if( now > show_pb_time )
                        {
                            pb.Value = i;
                            pb.Visibility = Visibility.Visible;
                        }
                    }

                    var end = now + 222;
                    do
                    {
                        if( ct.IsCancellationRequested ) return;

                        var segment = segments[i];

                        td.Range( segment.index, segment.length ).Style( styleInfo );

                    } while( ++i < last_i && Environment.TickCount < end );
                } );
            }
        }


        public static void ClearProperties( CancellationToken ct, ChangeEventHelper ceh, ProgressBar pb, TextData td, IList<Segment> segments0 )
        {
            // split into smaller segments

            var segments = new List<(int index, int length)>( segments0.Count );

            foreach( var segment in segments0 )
            {
                int j = segment.Index;
                int rem = segment.Length;

                do
                {
                    ct.ThrowIfCancellationRequested( );

                    int len = Math.Min( SEGMENT_LENGTH, rem );

                    segments.Add( (j, len) );

                    j += len;
                    rem -= len;

                } while( rem > 0 );
            }


            int show_pb_time = unchecked(Environment.TickCount + 333); // (ignore overflow)
            int last_i = segments.Count;

            if( pb != null )
            {
                ceh.Invoke( ct, ( ) =>
                {
                    pb.Visibility = Visibility.Hidden;
                    pb.Maximum = last_i;
                } );
            }

            //var rnd = new Random( );
            //segments = segments.OrderBy( s => rnd.Next() ).ToList( ); // just for fun

            for( int i = 0; i < last_i; )
            {
                ct.ThrowIfCancellationRequested( );

                ceh.Invoke( ct, ( ) =>
                {
                    var now = Environment.TickCount;

                    if( pb != null )
                    {
                        if( now > show_pb_time )
                        {
                            pb.Value = i;
                            pb.Visibility = Visibility.Visible;
                        }
                    }

                    var end = now + 222;
                    do
                    {
                        if( ct.IsCancellationRequested ) return;

                        var segment = segments[i];

                        td.Range( segment.index, segment.length ).ClearAllProperties( );

                    } while( ++i < last_i && Environment.TickCount < end );
                } );
            }
        }


        public static void ApplyProperty( CancellationToken ct, ChangeEventHelper ceh, TextData td, IList<Segment> segments0, DependencyProperty property, object value )
        {
            // split into smaller segments

            var segments = new List<(int index, int length)>( segments0.Count );

            foreach( var segment in segments0 )
            {
                int j = segment.Index;
                int rem = segment.Length;

                do
                {
                    ct.ThrowIfCancellationRequested( );

                    int len = Math.Min( SEGMENT_LENGTH, rem );

                    segments.Add( (j, len) );

                    j += len;
                    rem -= len;

                } while( rem > 0 );
            }


            int last_i = segments.Count;

            for( int i = 0; i < last_i; )
            {
                ct.ThrowIfCancellationRequested( );

                ceh.Invoke( ct, ( ) =>
                {
                    var now = Environment.TickCount;

                    var end = now + 222;
                    do
                    {
                        if( ct.IsCancellationRequested ) return;

                        var segment = segments[i];

                        td.Range( segment.index, segment.length ).ApplyPropertyValue( property, value );

                    } while( ++i < last_i && Environment.TickCount < end );
                } );
            }
        }
    }
}
