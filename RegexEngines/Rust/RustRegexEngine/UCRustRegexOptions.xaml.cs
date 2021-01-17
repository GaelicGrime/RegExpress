using RegexEngineInfrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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


namespace RustRegexEngineNs
{
	/// <summary>
	/// Interaction logic for UCRustRegexOptions.xaml
	/// </summary>
	partial class UCRustRegexOptions : UserControl
	{
		internal event EventHandler<RegexEngineOptionsChangedArgs> Changed;

		bool IsFullyLoaded = false;
		int ChangeCounter = 0;

		RustRegexOptions Options = new RustRegexOptions( );


		public UCRustRegexOptions( )
		{
			InitializeComponent( );

			DataContext = Options;
		}


		public RustRegexOptions GetSelectedOptions( )
		{
			if( Dispatcher.CheckAccess( ) )
				return Options;
			else
				return Options.Clone( );
		}


		internal void SetSelectedOptions( RustRegexOptions options )
		{
			try
			{
				++ChangeCounter;

				Options = options.Clone( );
				DataContext = Options;

				UpdateControls( );
			}
			finally
			{
				--ChangeCounter;
			}
		}


		private void UserControl_Loaded( object sender, RoutedEventArgs e )
		{
			if( IsFullyLoaded ) return;

			IsFullyLoaded = true;

			UpdateControls( );
		}


		private void CheckBox_Changed( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			Changed?.Invoke( null, new RegexEngineOptionsChangedArgs { PreferImmediateReaction = false } );
		}


		private void cbxStruct_SelectionChanged( object sender, SelectionChangedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			UpdateControls( );

			Changed?.Invoke( null, new RegexEngineOptionsChangedArgs { PreferImmediateReaction = true } );
		}


		private void tbx_TextChanged( object sender, TextChangedEventArgs e )
		{
			CheckBox_Changed( null, null );
		}


		void UpdateControls( )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			try
			{
				++ChangeCounter;

				string @struct = ( (ComboBoxItem)cbxStruct.SelectedItem )?.Tag?.ToString( );

				bool is_builder = @struct == "RegexBuilder";

				pnlRegexBuilderOptions.IsEnabled = is_builder;
				pnlRegexBuilderOptions.Opacity = pnlRegexBuilderOptions.IsEnabled ? 1 : 0.75;

				if( is_builder )
				{
					pnlRegexBuilderOptions.ClearValue( DataContextProperty ); // (to use inherited context)
				}
				else
				{
					pnlRegexBuilderOptions.DataContext = new RustRegexOptions( ); // (to show defaults)
				}
			}
			finally
			{
				--ChangeCounter;
			}

		}
	}
}
