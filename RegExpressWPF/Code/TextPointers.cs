using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Documents;


namespace RegExpressWPF.Code
{
	public sealed class TextPointers
	{
		internal readonly FlowDocument Doc;
		internal readonly int EolLength;


		public TextPointers( FlowDocument doc, int eolLength )
		{
			Debug.Assert( doc != null );
			Debug.Assert( eolLength == 1 || eolLength == 2 );

			Doc = doc;
			EolLength = eolLength;
		}


		public TextPointer GetTextPointer( int index )
		{
			Debug.Assert( index >= 0 );

			int remaining_index = index;

			foreach( var block in Doc.Blocks )
			{
				var tb = FindTextPointerB( (dynamic)block, ref remaining_index );

				if( tb != null ) return tb;
			}

			if( remaining_index == 0 )
			{
				return Doc.ContentEnd;
			}

			return Doc.ContentEnd; //?
		}


		public int GetIndex( TextPointer tp, LogicalDirection dir )
		{
			Debug.Assert( tp.IsInSameDocument( Doc.ContentStart ) );

			tp = tp.GetInsertionPosition( dir );
			TextElement parent = (TextElement)tp.Parent;

			int index = FindStartIndex( parent );
			if( index < 0 ) return -1;

			if( parent is Run )
			{
				return index + parent.ContentStart.GetOffsetToPosition( tp );
			}
			else
			{
				return index;
			}
		}


		TextPointer FindTextPointerB( Section section, ref int remainingIndex )
		{
			foreach( var block in section.Blocks )
			{
				var tp = FindTextPointerB( (dynamic)block, ref remainingIndex );

				if( tp != null ) return tp;
			}

			return null;
		}


		TextPointer FindTextPointerB( Paragraph para, ref int remainingIndex )
		{
			foreach( var inline in para.Inlines )
			{
				var tp = FindTextPointerI( (dynamic)inline, ref remainingIndex );
				if( tp != null ) return tp;
			}

			remainingIndex -= EolLength;
			if( remainingIndex < 0 ) return para.ContentEnd;

			return null;
		}


		TextPointer FindTextPointerI( Span span, ref int remainingIndex )
		{
			foreach( var inline in span.Inlines )
			{
				var tp = FindTextPointerI( (dynamic)inline, ref remainingIndex );
				if( tp != null ) return tp;
			}

			return null;
		}


		TextPointer FindTextPointerI( Run run, ref int remainingIndex )
		{
			Debug.Assert( !run.Text.Contains( '\r' ) );
			Debug.Assert( !run.Text.Contains( '\n' ) );

			var text_len = run.Text.Length;

			if( remainingIndex <= text_len )
			{
				return run.ContentStart.GetPositionAtOffset( remainingIndex );
			}

			remainingIndex -= text_len;

			return null;
		}


		TextPointer FindTextPointerI( LineBreak lb, ref int remainingIndex )
		{
			if( remainingIndex <= EolLength )
			{
				return lb.ElementStart;
			}

			remainingIndex -= EolLength;

			return null;
		}


		int FindStartIndex( TextElement el )
		{
			int index = 0;
			foreach( var block in Doc.Blocks )
			{
				if( FindStartIndexB( (dynamic)block, el, ref index ) ) return index;
			}

			return -1;
		}


		bool FindStartIndexB( Section section, TextElement el, ref int index )
		{
			if( object.ReferenceEquals( section, el ) ) return true;

			foreach( var block in section.Blocks )
			{
				if( FindStartIndexB( (dynamic)block, el, ref index ) ) return true;
			}

			return false;
		}


		bool FindStartIndexB( Paragraph para, TextElement el, ref int index )
		{
			if( object.ReferenceEquals( para, el ) ) return true;

			foreach( var inline in para.Inlines )
			{
				if( FindStartIndexI( (dynamic)inline, el, ref index ) ) return true;
			}

			index += EolLength;

			return false;
		}


		bool FindStartIndexI( Span span, TextElement el, ref int index )
		{
			if( object.ReferenceEquals( span, el ) ) return true;

			foreach( var inline in span.Inlines )
			{
				if( FindStartIndexI( (dynamic)inline, el, ref index ) ) return true;
			}

			return false;
		}


		bool FindStartIndexI( Run run, TextElement el, ref int index )
		{
			Debug.Assert( !run.Text.Contains( '\r' ) );
			Debug.Assert( !run.Text.Contains( '\n' ) );

			if( object.ReferenceEquals( run, el ) ) return true;

			index += run.Text.Length;

			return false;
		}


		bool FindStartIndexI( LineBreak lb, TextElement el, ref int index )
		{
			if( object.ReferenceEquals( lb, el ) ) return true;

			index += EolLength;

			return false;
		}

	}
}
