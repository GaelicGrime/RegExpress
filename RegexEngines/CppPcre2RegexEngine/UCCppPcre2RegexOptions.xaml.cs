using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

namespace CppPcre2RegexEngineNs
{
	/// <summary>
	/// Interaction logic for UCCppPcreRegexOptions.xaml
	/// </summary>
	public partial class UCCppPcre2RegexOptions : UserControl
	{
		internal event EventHandler Changed;


		bool IsFullyLoaded = false;
		int ChangeCounter = 0;


		public UCCppPcre2RegexOptions( )
		{
			InitializeComponent( );
		}


		internal object ToSerialisableObject( )
		{
			//.......
			return null;
		}


		internal void FromSerializableObject( object obj )
		{
			//......
		}

		private void CheckBox_Changed( object sender, RoutedEventArgs e )
		{

		}
	}
}
