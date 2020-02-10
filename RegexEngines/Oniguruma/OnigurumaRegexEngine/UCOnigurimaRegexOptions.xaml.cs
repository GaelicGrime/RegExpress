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

namespace OnigurumaRegexEngineNs
{
	/// <summary>
	/// Interaction logic for UCOnigurimaRegexOptions.xaml
	/// </summary>
	public partial class UCOnigurimaRegexOptions : UserControl
	{
		internal event EventHandler Changed;
		internal string[] CachedOptions; // (accessible from threads)


		bool IsFullyLoaded = false;
		int ChangeCounter = 0;


		public UCOnigurimaRegexOptions( )
		{
			InitializeComponent( );
		}


		internal string[] ExportOptions( )
		{
			return GetSelectedOptions( );
		}


		internal void ImportOptions( string[] options )
		{
			SetSelectedOptions( options );
		}


		internal string[] GetSelectedOptions( )
		{
			return
				new string[] { };
		}


		internal void SetSelectedOptions( string[] options )
		{
			try
			{
				++ChangeCounter;

				options = options ?? new string[] { };

				//......

			}
			finally
			{
				--ChangeCounter;
			}
		}


		private void UserControl_Loaded( object sender, RoutedEventArgs e )
		{
			if( IsFullyLoaded ) return;

			CachedOptions = GetSelectedOptions( );

			IsFullyLoaded = true;
		}


		private void CheckBox_Changed( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			CachedOptions = GetSelectedOptions( );

			Changed?.Invoke( null, null );
		}

	}
}
