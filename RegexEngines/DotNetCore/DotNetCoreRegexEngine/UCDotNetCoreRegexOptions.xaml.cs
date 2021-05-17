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


namespace DotNetCoreRegexEngineNs
{
	/// <summary>
	/// Interaction logic for UCDotNetCoreRegexOptions.xaml
	/// </summary>
	public partial class UCDotNetCoreRegexOptions : UserControl
	{
		internal event EventHandler<RegexEngineOptionsChangedArgs> Changed;

		bool IsFullyLoaded = false;
		int ChangeCounter = 0;

		DotNetCoreRegexOptions Options = new DotNetCoreRegexOptions( );


		public UCDotNetCoreRegexOptions( )
		{
			InitializeComponent( );

			DataContext = Options;
		}


		internal DotNetCoreRegexOptions GetSelectedOptions( )
		{
			if( Dispatcher.CheckAccess( ) )
				return Options;
			else
				return Options.Clone( );
		}


		internal void SetSelectedOptions( DotNetCoreRegexOptions options )
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
