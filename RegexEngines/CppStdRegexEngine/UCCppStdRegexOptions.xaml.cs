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


namespace CppStdRegexEngineNs
{
	/// <summary>
	/// Interaction logic for UCCppStdRegexOptions.xaml
	/// </summary>
	public partial class UCCppStdRegexOptions : UserControl
	{
		internal event EventHandler Changed;

		internal string[] CachedOptions; // (accessible from threads)


		public UCCppStdRegexOptions( )
		{
			InitializeComponent( );
		}


		internal object ToSerialisableObject( )
		{
			return GetSelectedOptions( );
		}


		internal void FromSerializableObject( object obj )
		{
			string[] arr = obj as string[];

			if( arr == null )
			{
				if( obj is object[] ) arr = ( (object[])obj ).OfType<string>( ).ToArray( );
			}

			SetSelectedOptions( arr );
		}


		internal string[] GetSelectedOptions( )
		{
			return pnl
					.Children
					.OfType<CheckBox>( )
					.Where( cb => cb.IsChecked == true )
					.Select( cb => cb.Tag.ToString( ) )
					.Concat( new[] { ( (ComboBoxItem)cbxGrammar.SelectedItem ).Tag.ToString( ) } )
					.ToArray( );
		}

		internal void SetSelectedOptions( string[] options )
		{
			options = options ?? new string[] { };

			var g = cbxGrammar.Items.Cast<ComboBoxItem>( ).FirstOrDefault( i => options.Contains( i.Tag.ToString( ) ) );
			cbxGrammar.SelectedItem = g;

			foreach( var cb in pnl.Children.OfType<CheckBox>( ) )
			{
				cb.IsChecked = options.Contains( cb.Tag.ToString( ) );
			}
		}


		private void cbxGrammar_SelectionChanged( object sender, SelectionChangedEventArgs e )
		{
			CachedOptions = GetSelectedOptions( );

			Changed?.Invoke( null, null );
		}

		private void CheckBox_Changed( object sender, RoutedEventArgs e )
		{
			CachedOptions = GetSelectedOptions( );

			Changed?.Invoke( null, null );
		}
	}
}
