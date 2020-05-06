using RegexEngineInfrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
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

	public class BaseTextData
	{
#pragma warning disable CA1051 // Do not declare visible instance fields

		public readonly string Text; // (lines are separated by EOL specified in the call of 'GetBaseTextData' and 'GetTextData',
		public readonly string Eol;  //  which is also kept in 'Eol')
		internal readonly TextPointers TextPointers; // (maps string index of 'Text' to 'TextPointer')

#pragma warning restore CA1051 // Do not declare visible instance fields

		internal BaseTextData( string text, string eol, TextPointers pointers )
		{
			Debug.Assert( eol.Length == pointers.EolLength );

			Text = text;
			Eol = eol;
			TextPointers = pointers;
		}
	}


	public sealed class TextData : BaseTextData
	{
#pragma warning disable CA1051 // Do not declare visible instance fields

		public readonly int SelectionStart;
		public readonly int SelectionEnd;

#pragma warning restore CA1051 // Do not declare visible instance fields

		internal TextData( string text, string eol, TextPointers pointers, int selectionStart, int selectionEnd )
			: base( text, eol, pointers )
		{
			SelectionStart = selectionStart;
			SelectionEnd = selectionEnd;
		}
	}


	public static class RtbUtilities
	{
		const int MAX_BLOCKING_TIME_MS = 222;
		const int MAX_SEGMENT_LENGTH = 100;


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


		public static BaseTextData GetBaseTextDataInternal( RichTextBox rtb, string eol )
		{
			DbgValidateEol( eol );

			FlowDocument doc = rtb.Document;
			RtbTextHelper th = new RtbTextHelper( doc, eol );

			string text = th.GetText( );

			return new BaseTextData( text, eol, new TextPointers( doc, eol.Length ) );
		}


		public static BaseTextData GetBaseTextDataFrom( RichTextBox rtb, BaseTextData btd, string eol )
		{
			DbgValidateEol( eol );
			DbgValidateEol( btd.Eol );
			//...Debug.Assert( !btd.OldPointers.Any( ) || btd.OldPointers.All( p => p.IsInSameDocument( rtb.Document.ContentStart ) ) );
			Debug.Assert( object.ReferenceEquals( rtb.Document, btd.TextPointers.Doc ) );

			string text;
			TextPointers textpointers;

			if( btd.Eol == eol )
			{
				text = btd.Text;
			}
			else
			{
				text = btd.Text.Replace( btd.Eol, eol );
			}

			if( btd.Eol.Length == eol.Length )
			{
				textpointers = btd.TextPointers;
			}
			else
			{
				textpointers = new TextPointers( rtb.Document, eol.Length );

			}

			return new BaseTextData( text, eol, textpointers );
		}


		public static TextData GetTextDataFrom( RichTextBox rtb, BaseTextData btd, string eol )
		{
			DbgValidateEol( eol );
			DbgValidateEol( btd.Eol );
			//...Debug.Assert( !btd.OldPointers.Any( ) || btd.OldPointers.All( p => p.IsInSameDocument( rtb.Document.ContentStart ) ) );
			Debug.Assert( object.ReferenceEquals( rtb.Document, btd.TextPointers.Doc ) );

			var (selection_start, selection_end) = GetSelection( rtb.Selection, btd.TextPointers );

			string new_text;
			TextPointers new_textpointers;

			if( btd.Eol == eol )
			{
				new_text = btd.Text;
			}
			else
			{
				new_text = btd.Text.Replace( btd.Eol, eol );
			}

			if( btd.Eol.Length == eol.Length )
			{
				new_textpointers = btd.TextPointers;
			}
			else
			{
				new_textpointers = new TextPointers( rtb.Document, eol.Length );

			}

			return new TextData( new_text, eol, new_textpointers, selection_start, selection_end );
		}


		static (int selection_start, int selection_end) GetSelection( TextSelection selection, TextPointers pointers )
		{
			int selection_start = Math.Max( 0, pointers.GetIndex( selection.Start, LogicalDirection.Backward ) );
			int selection_end = Math.Max( 0, pointers.GetIndex( selection.End, LogicalDirection.Forward ) );

			return (selection_start, selection_end);
		}


		public static void SafeSelect( RichTextBox rtb, TextData td, int selectionStart, int selectionEnd )
		{
			var tps = td.TextPointers.GetTextPointers( selectionStart, selectionEnd );

			rtb.Selection.Select( tps.Item1, tps.Item2 );
		}


		public static TextRange Range( this BaseTextData td, int start, int len )
		{
			var tps = td.TextPointers.GetTextPointers( start, start + len );
			var range = new TextRange( tps.Item1, tps.Item2 );

			return range;
		}


		public static TextRange Range0F( this BaseTextData td, int start, int len )
		{
			var tps = td.TextPointers.GetTextPointers( start, start + len );
			var range = new TextRange( tps.Item1, tps.Item2.GetInsertionPosition( LogicalDirection.Forward ) );

			return range;
		}


		public static TextRange Range0B( this BaseTextData td, int start, int len )
		{
			var tps = td.TextPointers.GetTextPointers( start, start + len );
			var range = new TextRange( tps.Item1, tps.Item2.GetInsertionPosition( LogicalDirection.Backward ) );

			return range;
		}


		public static TextRange RangeFB( this BaseTextData td, int start, int len )
		{
			var tps = td.TextPointers.GetTextPointers( start, start + len );
			var range = new TextRange( tps.Item1.GetInsertionPosition( LogicalDirection.Forward ), tps.Item2.GetInsertionPosition( LogicalDirection.Backward ) );

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


		public static bool ApplyStyle( ICancellable reh, ChangeEventHelper ceh, ProgressBar pb, TextData td, IReadOnlyList<(Segment segment, StyleInfo styleInfo)> segmentsAndStyles )
		{
			// split into smaller segments

			var segments = new List<(int index, int length, StyleInfo styleInfo)>( segmentsAndStyles.Count );

			foreach( var segment_and_style in segmentsAndStyles )
			{
				int j = segment_and_style.segment.Index;
				int rem = segment_and_style.segment.Length;

				do
				{
					if( reh.IsCancellationRequested ) return false;

					int len = Math.Min( MAX_SEGMENT_LENGTH, rem );

					segments.Add( (j, len, segment_and_style.styleInfo) );

					j += len;
					rem -= len;

				} while( rem > 0 );
			}


			int show_pb_time = unchecked(Environment.TickCount + 333); // (ignore overflow)
			int last_i = segments.Count;

			if( pb != null )
			{
				ceh.Invoke( CancellationToken.None, ( ) => //...
				{
					pb.Visibility = Visibility.Hidden;
					pb.Maximum = last_i;
				} );
			}

			//var rnd = new Random( );
			//segments = segments.OrderBy( s => rnd.Next() ).ToList( ); // just for fun

			//...
			//Debug.WriteLine( $"Total segments: {segments.Count}" );

			for( int i = 0; i < last_i; )
			{
				if( reh.IsCancellationRequested ) return false;

				ceh.Invoke( CancellationToken.None, ( ) =>
				{
					if( pb != null )
					{
						if( Environment.TickCount > show_pb_time )
						{
							pb.Value = i;
							pb.Visibility = Visibility.Visible;
						}
					}

					var end = Environment.TickCount + MAX_BLOCKING_TIME_MS;
					//int dbg_i = i;//...
					do
					{
						//if( reh.IsAnyRequested ) return false;

						var segment = segments[i];
						td.Range0F( segment.index, segment.length ).Style( segment.styleInfo );

					} while( ++i < last_i && Environment.TickCount < end );

					//Debug.WriteLine( $"Subsegments: {i - dbg_i}" ); //...

				} );
			}

			return true;
		}


		public static bool ApplyStyle( ICancellable reh, ChangeEventHelper ceh, ProgressBar pb, TextData td, IList<Segment> segments0, StyleInfo styleInfo )
		{
			// split into smaller segments

			var segments = new List<Segment>( segments0.Count );

			foreach( var segment in segments0 )
			{
				int j = segment.Index;
				int rem = segment.Length;

				do
				{
					if( reh.IsCancellationRequested ) return false;

					int len = Math.Min( MAX_SEGMENT_LENGTH, rem );

					segments.Add( new Segment( j, len ) );

					j += len;
					rem -= len;

				} while( rem > 0 );
			}


			int show_pb_time = unchecked(Environment.TickCount + 333); // (ignore overflow)
			int last_i = segments.Count;

			if( pb != null )
			{
				ceh.Invoke( CancellationToken.None, ( ) => //...
				{
					pb.Visibility = Visibility.Hidden;
					pb.Maximum = last_i;
				} );
			}

			//var rnd = new Random( );
			//segments = segments.OrderBy( s => rnd.Next( ) ).ToList( ); // just for fun

			//...
			//Debug.WriteLine( $"Total segments: {segments.Count}" );

			for( int i = 0; i < last_i; )
			{
				if( reh.IsCancellationRequested ) return false;

				ceh.Invoke( CancellationToken.None, ( ) =>
				{
					if( pb != null )
					{
						if( Environment.TickCount > show_pb_time )
						{
							pb.Value = i;
							pb.Visibility = Visibility.Visible;
						}
					}

					var end = Environment.TickCount + MAX_BLOCKING_TIME_MS;
					//int dbg_i = i;//...
					do
					{
						var segment = segments[i];
						td.Range0F( segment.Index, segment.Length ).Style( styleInfo );

					} while( ++i < last_i && Environment.TickCount < end );

					//Debug.WriteLine( $"Subsegments: {i - dbg_i}" ); //...

				} );
			}

			return true;
		}


		// This seems to be too slow compared with ApplyStyle...
		[Obsolete( "Too slow. Try 'ApplyStyle'.", true )]
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

					int len = Math.Min( MAX_SEGMENT_LENGTH, rem );

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
					if( pb != null )
					{
						if( Environment.TickCount > show_pb_time )
						{
							pb.Value = i;
							pb.Visibility = Visibility.Visible;
						}
					}

					var end = Environment.TickCount + MAX_BLOCKING_TIME_MS;
					do
					{
						ct.ThrowIfCancellationRequested( );

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

					int len = Math.Min( MAX_SEGMENT_LENGTH, rem );

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
					var end = Environment.TickCount + MAX_BLOCKING_TIME_MS;
					do
					{
						ct.ThrowIfCancellationRequested( );

						var segment = segments[i];
						td.Range( segment.index, segment.length ).ApplyPropertyValue( property, value );

					} while( ++i < last_i && Environment.TickCount < end );
				} );
			}
		}


		[Conditional( "DEBUG" )]
		public static void DbgValidateEol( string eol )
		{
			Debug.Assert( eol == "\r\n" || eol == "\n\r" || eol == "\r" || eol == "\n" );
		}
	}
}
