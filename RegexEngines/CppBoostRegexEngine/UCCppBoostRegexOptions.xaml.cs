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


namespace CppBoostRegexEngineNs
{
	/// <summary>
	/// Interaction logic for UCCppBoostRegexOptions.xaml
	/// </summary>
	public partial class UCCppBoostRegexOptions : UserControl
	{
		internal event EventHandler Changed;
		internal string[] CachedOptions; // (accessible from threads)


		bool IsFullyLoaded = false;
		int ChangeCounter = 0;


		public UCCppBoostRegexOptions( )
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
			var cbs = pnl1.Children.OfType<CheckBox>( ).Concat( pnl2.Children.OfType<CheckBox>( ) );

			return cbs
					.Where( cb => cb.IsChecked == true )
					.Select( cb => cb.Tag.ToString( ) )
					.Concat( new[] { ( (ComboBoxItem)cbxGrammar.SelectedItem ).Tag.ToString( ) } )
					.ToArray( );
		}


		internal void SetSelectedOptions( string[] options )
		{
			try
			{
				++ChangeCounter;

				options = options ?? new string[] { };

				var g = cbxGrammar.Items.Cast<ComboBoxItem>( ).FirstOrDefault( i => options.Contains( i.Tag.ToString( ) ) );
				cbxGrammar.SelectedItem = g;

				var cbs = pnl1.Children.OfType<CheckBox>( ).Concat( pnl2.Children.OfType<CheckBox>( ) );

				foreach( var cb in cbs )
				{
					cb.IsChecked = options.Contains( cb.Tag.ToString( ) );
				}
			}
			finally
			{
				--ChangeCounter;
			}
		}


		internal GrammarEnum GetGrammar( ) // (accessible from threads)
		{
			string grammar_s = Enum.GetNames( typeof( GrammarEnum ) ).FirstOrDefault( n => n != "None" && CachedOptions.Contains( n ) );
			if( grammar_s == null ) return GrammarEnum.None;

			return (GrammarEnum)Enum.Parse( typeof( GrammarEnum ), grammar_s );
		}


		internal bool GetModX( ) // (accessible from threads)
		{
			return CachedOptions.Contains( "mod_x" );
		}


		private void UserControl_Loaded( object sender, RoutedEventArgs e )
		{
			if( IsFullyLoaded ) return;

			CachedOptions = GetSelectedOptions( );

			IsFullyLoaded = true;
		}


		private void cbxGrammar_SelectionChanged( object sender, SelectionChangedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			CachedOptions = GetSelectedOptions( );

			Changed?.Invoke( null, null );
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
