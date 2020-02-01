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


namespace Re2RegexEngineNs
{
	/// <summary>
	/// Interaction logic for UCRe2RegexOptions.xaml
	/// </summary>
	public partial class UCRe2RegexOptions : UserControl
	{
		internal event EventHandler Changed;
		internal string[] CachedOptions; // (accessible from threads)


		bool IsFullyLoaded = false;
		int ChangeCounter = 0;


		public UCRe2RegexOptions( )
		{
			InitializeComponent( );

			// insert checkboxes

			List<Re2RegexInterop.OptionInfo> compile_options = Re2RegexInterop.Matcher.GetOptions( );

			foreach( var o in compile_options )
			{
				var cb = new CheckBox
				{
					Tag = o.FlagName,
					Content = ( o.FlagName + " – " + o.Note ).Replace( "_", "__" ),

					// Does not seem useful:
					//IsChecked = o.DefaultValue
				};

				pnlOptions.Children.Add( cb );
			}
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
			return
				pnlOptions.Children.OfType<CheckBox>( )
					.Where( cb => cb.IsChecked == true )
					.Select( cb => cb.Tag.ToString( ) )
					.ToArray( );
		}


		internal void SetSelectedOptions( string[] options )
		{
			try
			{
				++ChangeCounter;

				options = options ?? new string[] { };

				foreach( var cb in pnlOptions.Children.OfType<CheckBox>( ) )
				{
					cb.IsChecked = options.Contains( cb.Tag.ToString( ) );
				}
			}
			finally
			{
				--ChangeCounter;
			}
		}


		internal bool IsOptionSelected( string tag )
		{
			return CachedOptions.Contains( tag );
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
