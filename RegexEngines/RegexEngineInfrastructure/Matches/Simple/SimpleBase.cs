﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure.Matches.Simple
{
	public abstract class SimpleBase
	{
		protected readonly ISimpleTextGetter TextGetter;


		protected SimpleBase( int index, int length, ISimpleTextGetter textGetter )
		{
			Index = TextIndex = index;
			Length = TextLength = length;
			TextGetter = textGetter;
		}


		protected SimpleBase( int index, int length, int textIndex, int textLength, ISimpleTextGetter textGetter )
		{
			Index = index;
			Length = length;
			TextIndex = textIndex;
			TextLength = textLength;
			TextGetter = textGetter;
		}


		public int Index { get; }

		public int Length { get; }

		public int TextIndex { get; }

		public int TextLength { get; }

		public string Value => Index < 0 ? String.Empty : TextGetter.GetText( TextIndex, TextLength );
	}
}
