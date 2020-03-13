using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure.Matches.Simple
{
	public sealed class SimpleMatch : SimpleBase, IMatch
	{
		readonly List<IGroup> mGroups = new List<IGroup>( );


		private SimpleMatch( int index, int length, ISimpleTextGetter textGetter )
			: base( index, length, textGetter )
		{
		}


		public static SimpleMatch Create( int index, int length, ISimpleTextGetter textGetter )
		{
			return new SimpleMatch( index, length, textGetter );
		}


		#region IMatch

		public IEnumerable<IGroup> Groups => mGroups;

		public bool Success { get; } = true; // TODO: reconsider the inheritance

		public string Name { get; } // TODO: reconsider the inheritance

		#endregion


		#region IGroup

		public IEnumerable<ICapture> Captures
		{
			get
			{
				// Not expected to be called.

				throw new InvalidOperationException( );
			}
		}

		#endregion


		public SimpleGroup AddGroup( int index, int length, bool success, string name )
		{
			var group = new SimpleGroup( index, length, TextGetter, success, name );
			mGroups.Add( group );

			return group;
		}


		public void SetGroupName(int index, string name)
		{
			( (SimpleGroup)mGroups[index] ).SetName( name );
		}


	}
}
