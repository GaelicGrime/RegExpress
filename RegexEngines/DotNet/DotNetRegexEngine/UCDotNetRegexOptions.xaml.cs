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
	public partial class UCDotNetRegexOptions : UserControl
	{
		internal event EventHandler<RegexEngineOptionsChangedArgs> Changed;

		bool IsFullyLoaded = false;
		int ChangeCounter = 0;
		static readonly string[] RegexOptionsNames = Enum.GetNames( typeof( RegexOptions ) ); //.........

		DotNetRegexOptions Options = new DotNetRegexOptions( );


		public UCDotNetRegexOptions( )
		{
			InitializeComponent( );

			DataContext = Options;
		}


		/*
		internal DotNetRegexOptions ExportOptions( )
		{
			return Options;
		}


		internal void ImportOptions( DotNetRegexOptions options )
		{
			RegexOptions options = RegexOptions.None;
			TimeSpan timeout = TimeSpan.FromSeconds( 10 );

			options = Enum.GetNames( typeof( RegexOptions ) )
				.Intersect( options0, StringComparer.InvariantCultureIgnoreCase )
				.Select( s => (RegexOptions)Enum.Parse( typeof( RegexOptions ), s, ignoreCase: true ) )
				.Aggregate( RegexOptions.None, ( a, o ) => a | o );

			string timeout_s =
				options0
				.FirstOrDefault( s => s.StartsWith( "timeout:" ) )
				?.Substring( "timeout:".Length );

			if( TimeSpan.TryParse( timeout_s, CultureInfo.InvariantCulture, out TimeSpan t ) ) timeout = t;


			SetSelectedOptions( options );
			SetTimeout( timeout );
		}
		*/


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
				++ChangeCounter; //............

				//pnl.DataContext = null;
				Options = options.Clone( );
				DataContext = Options;
			}
			finally
			{
				--ChangeCounter;
			}
		}

		//.........
		//internal TimeSpan GetTimeout( ) //..........
		//{
		//	return TimeSpan.Parse( ( (ComboBoxItem)cbxTimeout.SelectedItem ).Tag.ToString( ), CultureInfo.InvariantCulture );
		//}


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

			//............
			//if( TimeSpan.TryParse( ( (ComboBoxItem)cbxTimeout.SelectedItem ).Tag.ToString( ), CultureInfo.InvariantCulture, out TimeSpan t ) )
			//{
			//	CachedTimeout = t;
			//}

			Changed?.Invoke( this, new RegexEngineOptionsChangedArgs { PreferImmediateReaction = true } );
		}
	}
}
