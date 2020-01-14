using RegexEngineInfrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CppBoostRegexEngineNs
{
	public class CppBoostRegexEngine : IRegexEngine
	{
		readonly UCCppBoostRegexOptions OptionsControl;


		public CppBoostRegexEngine( )
		{
			OptionsControl = new UCCppBoostRegexOptions( );
			OptionsControl.Changed += OptionsControl_Changed;
		}


		#region IRegexEngine

		public string Id => "CppBoostRegex";

		public string Name => "C++ Boost Regex";

		public event EventHandler OptionsChanged;


		public Control GetOptionsControl( )
		{
			return OptionsControl;
		}


		public object SerializeOptions( )
		{
			return OptionsControl.ToSerialisableObject( );
		}


		public void DeserializeOptions( object obj )
		{
			OptionsControl.FromSerializableObject( obj );
		}


		public IMatcher ParsePattern( string pattern )
		{
			var selected_options = OptionsControl.CachedOptions;

			return new CppBoostRegexInterop.CppMatcher( pattern, selected_options );
		}

		#endregion IRegexEngine


		private void OptionsControl_Changed( object sender, EventArgs e )
		{
			OptionsChanged?.Invoke( this, null );
		}

	}
}
