using RegexEngineInfrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
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


namespace DotNetRegexEngineNs
{
	/// <summary>
	/// Interaction logic for UCDotNetRegexOptions.xaml
	/// </summary>
	partial class UCDotNetRegexOptions : UserControl
	{
		internal event EventHandler<RegexEngineOptionsChangedArgs> Changed;

		bool IsFullyLoaded = false;
		int ChangeCounter = 0;

		DotNetRegexOptions Options = new DotNetRegexOptions( );


		public UCDotNetRegexOptions( )
		{
			InitializeComponent( );

			DataContext = Options;
		}


		internal DotNetRegexOptions GetSelectedOptions( )
		{
			if( Dispatcher.CheckAccess( ) )
				return Options;
			else
				return Options.Clone( );
		}


		internal void SetSelectedOptions( DotNetRegexOptions options )
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


		private void CbOption_CheckedChanged( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			Changed?.Invoke( this, new RegexEngineOptionsChangedArgs { PreferImmediateReaction = false } );
		}


		private void cbxTimeout_SelectionChanged( object sender, SelectionChangedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			Changed?.Invoke( this, new RegexEngineOptionsChangedArgs { PreferImmediateReaction = true } );
		}
	}
}
