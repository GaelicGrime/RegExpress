using RegexEngineInfrastructure;
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


namespace IcuRegexEngineNs
{
	/// <summary>
	/// Interaction logic for UCIcuRegexOptions.xaml
	/// </summary>
	partial class UCIcuRegexOptions : UserControl
	{
		internal event EventHandler<RegexEngineOptionsChangedArgs> Changed;

		bool IsFullyLoaded = false;
		int ChangeCounter = 0;

		IcuRegexOptions Options = new IcuRegexOptions( );


		public UCIcuRegexOptions( )
		{
			InitializeComponent( );

			DataContext = Options;
		}


		public IcuRegexOptions GetSelectedOptions( )
		{
			if( Dispatcher.CheckAccess( ) )
				return Options;
			else
				return Options.Clone( );
		}


		internal void SetSelectedOptions( IcuRegexOptions options )
		{
			try
			{
				++ChangeCounter;

				Options = options.Clone( );
				DataContext = Options;
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
		}


		private void CheckBox_Changed( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			Changed?.Invoke( null, new RegexEngineOptionsChangedArgs { PreferImmediateReaction = false } );
		}


		private void tbxIterationLimit_TextChanged( object sender, TextChangedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			Changed?.Invoke( null, new RegexEngineOptionsChangedArgs { PreferImmediateReaction = false } );
		}

	}
}
