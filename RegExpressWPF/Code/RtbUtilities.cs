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
		public readonly IReadOnlyList<TextPointer> Pointers; // (maps string index of 'Text' to 'TextPointer')

#pragma warning restore CA1051 // Do not declare visible instance fields


		public BaseTextData( string text, string eol, IReadOnlyList<TextPointer> pointers )
		{
			Text = text;
			Eol = eol;
			Pointers = pointers;
		}


		public TextPointer SafeGetPointer( int i )
		{
			Debug.Assert( Pointers.Any( ) );
			Debug.Assert( i >= 0 ); // covered by this function, but not really expected

			if( i >= Pointers.Count ) i = Pointers.Count - 1;
			if( i < 0 ) i = 0;

			return Pointers[i];
		}
	}


	public sealed class TextData : BaseTextData
	{
#pragma warning disable CA1051 // Do not declare visible instance fields

		public readonly int SelectionStart;
		public readonly int SelectionEnd;

#pragma warning restore CA1051 // Do not declare visible instance fields

		internal TextData( string text, string eol, IReadOnlyList<TextPointer> pointers, int selectionStart, int selectionEnd )
			: base( text, eol, pointers )
		{
			SelectionStart = selectionStart;
			SelectionEnd = selectionEnd;
		}
	}


	public sealed class SimpleTextData
	{
#pragma warning disable CA1051 // Do not declare visible instance fields

		public readonly string Text;
		public readonly string Eol;
		public readonly int SelectionStart;
		public readonly int SelectionEnd;

#pragma warning restore CA1051 // Do not declare visible instance fields

		internal SimpleTextData( string text, string eol, int selectionStart, int selectionEnd )
		{
			Text = text;
			Eol = eol;
			SelectionStart = selectionStart;
			SelectionEnd = selectionEnd;
		}
	}


	public static class RtbUtilities
	{
		class TraversalData
		{
			internal string Eol;

			internal StringBuilder Sb = new StringBuilder( );
			internal List<TextPointer> Pointers = new List<TextPointer>( );
			internal Paragraph PrevPara = null;
		}


		class SimpleTraversalData
		{
			internal string Eol;
			internal TextSelection Selection;

			internal StringBuilder Sb = new StringBuilder( );
			internal Paragraph PrevPara = null;
			internal int SelectionStart = 0;
			internal int SelectionEnd = 0;
		}


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

			TraversalData data = new TraversalData
			{
				Eol = eol,
			};

			ProcessBlocks( data, doc.Blocks );

			data.Pointers.Add( doc.ContentEnd );
			string text = data.Sb.ToString( );

			Debug.Assert( text.Length + 1 == data.Pointers.Count );

			return new BaseTextData( text, eol, data.Pointers );
		}


		public static TextData GetTextDataFrom( RichTextBox rtb, BaseTextData btd, string eol )
		{
			DbgValidateEol( eol );
			DbgValidateEol( btd.Eol );
			Debug.Assert( !btd.Pointers.Any( ) || btd.Pointers.All( p => p.IsInSameDocument( rtb.Document.ContentStart ) ) );

			if( btd.Eol.Length == eol.Length )
			{
				string text;

				if( btd.Eol == eol )
				{
					text = btd.Text;
				}
				else
				{
					text = btd.Text.Replace( btd.Eol, eol );
				}

				var (selection_start, selection_end) = GetSelection( rtb.Selection, btd.Pointers );

				Debug.Assert( text.Length <= btd.Pointers.Count );

				return new TextData( text, eol, btd.Pointers, selection_start, selection_end );
			}
			else
			{
				string old_text = btd.Text;
				string old_eol = btd.Eol;
				IReadOnlyList<TextPointer> old_pointers = btd.Pointers;
				int old_eol_length = old_eol.Length;
				int new_eol_length = eol.Length;
				int prev_i = 0;
				var sb = new StringBuilder( old_text.Length );
				var pointers = new List<TextPointer>( btd.Pointers.Count );

				for( int i = old_text.IndexOf( old_eol, StringComparison.Ordinal );
					i >= 0;
					i = old_text.IndexOf( old_eol, prev_i = i + old_eol_length, StringComparison.Ordinal ) )
				{
					sb.Append( old_text, prev_i, i - prev_i );
					sb.Append( eol );

					for( int k = prev_i; k < i; ++k ) pointers.Add( old_pointers[k] );
					for( int k = 0; k < new_eol_length; ++k ) pointers.Add( old_pointers[i] );
				}

				// last segment
				sb.Append( old_text, prev_i, old_text.Length - prev_i );
				for( int k = prev_i; k < old_pointers.Count; ++k ) pointers.Add( old_pointers[k] ); // including end-of-document

				string text = sb.ToString( );
				Debug.Assert( text.Length + 1 == pointers.Count );

				var (selection_start, selection_end) = GetSelection( rtb.Selection, pointers );

				return new TextData( text, eol, pointers, selection_start, selection_end );
			}
		}


		internal static SimpleTextData GetSimpleTextDataInternal( RichTextBox rtb, string eol )
		{
			DbgValidateEol( eol );

			FlowDocument doc = rtb.Document;

			SimpleTraversalData data = new SimpleTraversalData
			{
				Eol = eol,
				Selection = rtb.Selection,
			};

			ProcessBlocks( data, doc.Blocks );

			return new SimpleTextData( data.Sb.ToString( ), data.Eol, data.SelectionStart, data.SelectionEnd );
		}


		internal static SimpleTextData GetSimpleTextDataFrom( RichTextBox rtb, BaseTextData btd, string eol )
		{
			DbgValidateEol( eol );

			var td = GetTextDataFrom( rtb, btd, eol );

			return new SimpleTextData( td.Text, td.Eol, td.SelectionStart, td.SelectionEnd );
		}


		internal static SimpleTextData GetSimpleTextDataFrom( SimpleTextData std, string eol )
		{
			DbgValidateEol( eol );
			DbgValidateEol( std.Eol );

			if( std.Eol.Length == eol.Length )
			{
				if( std.Eol == eol )
				{
					return std;
				}
				else
				{
					string text = std.Text.Replace( std.Eol, eol );

					return new SimpleTextData( text, eol, std.SelectionStart, std.SelectionEnd );
				}
			}
			else
			{
				string old_text = std.Text;
				int old_eol_len = std.Eol.Length;
				int diff = eol.Length - old_eol_len;
				int selectionStart = std.SelectionStart;
				int selectionEnd = std.SelectionEnd;
				int prev_i = 0;
				var sb = new StringBuilder( old_text.Length );

				for( int i = old_text.IndexOf( std.Eol, StringComparison.Ordinal );
					i >= 0;
					i = old_text.IndexOf( std.Eol, prev_i = i + old_eol_len, StringComparison.Ordinal ) )
				{
					sb.Append( old_text, prev_i, i - prev_i );
					sb.Append( eol );

					if( selectionStart > i )
					{
						selectionStart += diff;
					}

					if( selectionEnd > i )
					{
						selectionEnd += diff;
					}
				}

				// last segment
				sb.Append( old_text, prev_i, old_text.Length - prev_i );

				return new SimpleTextData( sb.ToString( ), eol, selectionStart, selectionEnd );
			}
		}


		static (int selection_start, int selection_end) GetSelection( TextSelection selection, IReadOnlyList<TextPointer> pointers )
		{
			int selection_start = Math.Max( 0, FindNearestAfter( pointers, selection.Start ) );
			int selection_end = Math.Max( 0, FindNearestAfter( pointers, selection.End ) );

			return (selection_start, selection_end);
		}


		public static int FindNearestBefore( IReadOnlyList<TextPointer> pointers, TextPointer target )
		{
			if( pointers.Count == 0 ) return -1;

			Debug.Assert( pointers[0].IsInSameDocument( target ) );

			int left = 0;
			int right = pointers.Count( ) - 1;
			int last_good = -1;

			do
			{
				int mid = ( left + right ) / 2;

				int cmp = pointers[mid].CompareTo( target );

				if( cmp == 0 ) return mid;

				if( cmp < 0 )
				{
					last_good = mid;
					left = mid + 1;
				}
				else
				{
					right = mid - 1;
				}
			} while( left <= right );

			return last_good;
		}


		public static int FindNearestAfter( IReadOnlyList<TextPointer> pointers, TextPointer target )
		{
			if( pointers.Count == 0 ) return -1;

			Debug.Assert( pointers[0].IsInSameDocument( target ) );

			int left = 0;
			int right = pointers.Count( ) - 1;
			int last_good = -1;

			do
			{
				int mid = ( left + right ) / 2;

				int cmp = pointers[mid].CompareTo( target );

				if( cmp == 0 ) return mid;

				if( cmp < 0 )
				{
					left = mid + 1;
				}
				else
				{
					last_good = mid;
					right = mid - 1;
				}
			} while( left <= right );

			return last_good;
		}


		public static int Find( IReadOnlyList<TextPointer> pointers, TextPointer target )
		{
			if( pointers.Count == 0 ) return -1;

			Debug.Assert( pointers[0].IsInSameDocument( target ) );

			int left = 0;
			int right = pointers.Count( ) - 1;

			do
			{
				int mid = ( left + right ) / 2;

				int cmp = pointers[mid].CompareTo( target );

				if( cmp == 0 ) return mid;

				if( cmp < 0 )
				{
					left = mid + 1;
				}
				else
				{
					right = mid - 1;
				}
			} while( left <= right );

			return -1;
		}


		public static void SafeSelect( RichTextBox rtb, TextData td, int selectionStart, int selectionEnd )
		{
			Debug.Assert( td.Pointers.Any( ) );
			Debug.Assert( selectionStart < td.Pointers.Count );
			Debug.Assert( selectionEnd < td.Pointers.Count );

			if( td.Pointers.Any( ) )
			{
				rtb.Selection.Select( td.SafeGetPointer( selectionStart ), td.SafeGetPointer( selectionEnd ) );
			}
		}


		static void ProcessBlocks( TraversalData data, IEnumerable<Block> blocks )
		{
			foreach( var block in blocks )
			{
				switch( block )
				{
				case Section section:
					ProcessBlocks( data, section.Blocks );
					break;
				case Paragraph para:
				{
					if( data.PrevPara != null )
					{
						data.Sb.Append( data.Eol );
						for( var i = 0; i < data.Eol.Length; ++i ) data.Pointers.Add( data.PrevPara.ContentEnd );
					}
					ProcessInlines( data, para.Inlines );
					data.PrevPara = para;
				}
				break;
				default:
					Debug.Assert( false );
					break;
				}
			}
		}


		static void ProcessBlocks( SimpleTraversalData data, IEnumerable<Block> blocks )
		{
			foreach( var block in blocks )
			{
				switch( block )
				{
				case Section section:
					ProcessBlocks( data, section.Blocks );
					break;
				case Paragraph para:
				{
					if( data.PrevPara != null )
					{
						data.Sb.Append( data.Eol );
					}
					ProcessInlines( data, para.Inlines );
					data.PrevPara = para;
				}
				break;
				default:
					Debug.Assert( false );
					break;
				}
			}
		}


		static void ProcessInlines( TraversalData data, IEnumerable<Inline> inlines )
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
							data.Sb.Append( data.Eol );
							for( int j = 0; j < data.Eol.Length; ++j ) data.Pointers.Add( p );
							next_i = i + 1;
							if( next_i < run.Text.Length && run.Text[next_i] == '\n' ) ++i; // skip
							break;
						case '\n':
							data.Sb.Append( data.Eol );
							for( int j = 0; j < data.Eol.Length; ++j ) data.Pointers.Add( p );
							next_i = i + 1;
							if( next_i < run.Text.Length && run.Text[next_i] == '\r' ) ++i; // skip
							break;
						default:
							data.Sb.Append( c );
							data.Pointers.Add( p );
							break;
						}
					}
					break;
				case Span span:
					ProcessInlines( data, span.Inlines );
					break;
				case LineBreak lb:
					data.Sb.Append( data.Eol );
					for( int j = 0; j < data.Eol.Length; ++j ) data.Pointers.Add( lb.ContentStart );
					break;
				}
			}
		}


		static void ProcessInlines( SimpleTraversalData data, IEnumerable<Inline> inlines )
		{
			foreach( Inline inline in inlines )
			{
				switch( inline )
				{
				case Run run:
					var start = run.ContentStart;

					if( object.ReferenceEquals( data.Selection.Start.Parent, run ) )
					{
						data.SelectionStart = data.Sb.Length + start.GetOffsetToPosition( data.Selection.Start );
					}
					if( object.ReferenceEquals( data.Selection.End.Parent, run ) )
					{
						data.SelectionEnd = data.Sb.Length + start.GetOffsetToPosition( data.Selection.End );
					}

					for( int i = 0; i < run.Text.Length; ++i )
					{
						var c = run.Text[i];
						int next_i;

						switch( c )
						{
						case '\r':
							data.Sb.Append( data.Eol );
							next_i = i + 1;
							if( next_i < run.Text.Length && run.Text[next_i] == '\n' ) ++i; // skip
							break;
						case '\n':
							data.Sb.Append( data.Eol );
							next_i = i + 1;
							if( next_i < run.Text.Length && run.Text[next_i] == '\r' ) ++i; // skip
							break;
						default:
							data.Sb.Append( c );
							break;
						}
					}
					break;
				case Span span:
					ProcessInlines( data, span.Inlines );
					break;
				case LineBreak lb:
					data.Sb.Append( data.Eol );
					break;
				}
			}
		}


		public static TextRange Range( this BaseTextData td, int start, int len )
		{
			var range = new TextRange( td.Pointers[start], td.Pointers[start + len] );

			return range;
		}


		public static TextRange Range0F( this BaseTextData td, int start, int len )
		{
			var range = new TextRange( td.Pointers[start], td.Pointers[start + len].GetInsertionPosition( LogicalDirection.Forward ) );

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


		const int SEGMENT_LENGTH = 7000;


		public static void ApplyStyle( CancellationToken ct, ChangeEventHelper ceh, ProgressBar pb, TextData td, IReadOnlyList<(Segment segment, StyleInfo styleInfo)> segmentsAndStyles )
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

			//...
			//Debug.WriteLine( $"Total segments: {segments.Count}" );

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

					var end = Environment.TickCount + 22;
					int dbg_i = i;//...
					do
					{
						ct.ThrowIfCancellationRequested( );

						var segment = segments[i];
						td.Range0F( segment.index, segment.length ).Style( segment.styleInfo );

					} while( ++i < last_i && Environment.TickCount < end );

					//Debug.WriteLine( $"Subsegments: {i - dbg_i}" ); //...

				} );
			}
		}


		public static void ApplyStyle( CancellationToken ct, ChangeEventHelper ceh, ProgressBar pb, TextData td, IList<Segment> segments0, StyleInfo styleInfo )
		{
			// split into smaller segments

			var segments = new List<Segment>( segments0.Count );

			foreach( var segment in segments0 )
			{
				int j = segment.Index;
				int rem = segment.Length;

				do
				{
					ct.ThrowIfCancellationRequested( );

					int len = Math.Min( SEGMENT_LENGTH, rem );

					segments.Add( new Segment( j, len ) );

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
			//segments = segments.OrderBy( s => rnd.Next( ) ).ToList( ); // just for fun

			//...
			//Debug.WriteLine( $"Total segments: {segments.Count}" );

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

					var end = Environment.TickCount + 22;
					int dbg_i = i;//...
					do
					{
						//ct.ThrowIfCancellationRequested( );

						var segment = segments[i];
						td.Range0F( segment.Index, segment.Length ).Style( styleInfo );

					} while( ++i < last_i && Environment.TickCount < end );

					//Debug.WriteLine( $"Subsegments: {i - dbg_i}" ); //...

				} );
			}
		}


		public static bool ApplyStyle( WaitHandle wh, ChangeEventHelper ceh, ProgressBar pb, TextData td, IList<Segment> segments0, StyleInfo styleInfo )
		{
			// split into smaller segments

			var segments = new List<Segment>( segments0.Count );

			foreach( var segment in segments0 )
			{
				int j = segment.Index;
				int rem = segment.Length;

				do
				{
					if( wh.WaitOne( 0 ) ) return false;

					int len = Math.Min( SEGMENT_LENGTH, rem );

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
				if( wh.WaitOne( 0 ) ) return false;

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

					var end = Environment.TickCount + 22;
					int dbg_i = i;//...
					do
					{
						//ct.ThrowIfCancellationRequested( );

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
					if( pb != null )
					{
						if( Environment.TickCount > show_pb_time )
						{
							pb.Value = i;
							pb.Visibility = Visibility.Visible;
						}
					}

					var end = Environment.TickCount + 22;
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
					var end = Environment.TickCount + 22;
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
