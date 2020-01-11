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

namespace CppRegexEngineControls
{
	/// <summary>
	/// Interaction logic for UCCppRegexOptions.xaml
	/// </summary>
	public partial class UCCppRegexOptions : UserControl
	{
		public UCCppRegexOptions( )
		{
			InitializeComponent( );
		}


		public string[] GetSelectedOptions( )
		{
			return pnl.Children.OfType<CheckBox>( ).Select( cb => cb.Tag.ToString( ) ).Concat( new[] { ( (ComboBoxItem)cbxGrammar.SelectedItem ).Tag.ToString( ) } ).ToArray( );
		}


		public void SetSelectedOptions( string[] options )
		{
			var g = cbxGrammar.Items.Cast<ComboBoxItem>( ).FirstOrDefault( i => options.Contains( i.Tag.ToString( ) ) );
			cbxGrammar.SelectedItem = g;

			foreach( var cb in pnl.Children.OfType<CheckBox>( ) )
			{
				cb.IsChecked = options.Contains( cb.Tag.ToString( ) );
			}
		}
	}
}
