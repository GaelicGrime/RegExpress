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

		// (accessible from threads)
		internal RegexOptions CachedRegexOptions;
		internal TimeSpan CachedTimeout;

		bool IsFullyLoaded = false;
		int ChangeCounter = 0;
		static readonly string[] RegexOptionsNames = Enum.GetNames( typeof( RegexOptions ) );



		public UCDotNetRegexOptions( )
		{
			InitializeComponent( );
		}


		internal string[] ExportOptions( )
		{
			return pnl
				.Children
				.OfType<CheckBox>( )
				.Where( cb => cb.IsChecked == true && RegexOptionsNames.Contains( cb.Tag?.ToString( ) ) )
				.Select( cb => cb.Tag.ToString( ) )
				.Concat( new[] { $"timeout:{CachedTimeout.ToString( "c", CultureInfo.InvariantCulture )}" } )
				.ToArray( );
		}


		internal void ImportOptions( string[] options0 )
		{
			if( options0.Length == 1 && options0[0].StartsWith( "OldRegexOptionsEnum:" ) )
			{
				// special backward-compatibility case; convert a number (treated as 'RegexOptions') to array of names

				string number_s = options0[0].Substring( "OldRegexOptionsEnum:".Length );
				if( int.TryParse( number_s, out int number ) )
				{
					RegexOptions opt = (RegexOptions)number;

					options0 = Enum.Format( typeof( RegexOptions ), opt, "G" ).Split( ',' ).Select( s => s.Trim( ) ).ToArray( );
				}
			}

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


		internal RegexOptions GetSelectedOptions( )
		{
			return
				pnl
					.Children
					.OfType<CheckBox>( )
					.Where( cb => cb.IsChecked == true && RegexOptionsNames.Contains( cb.Tag?.ToString( ) ) )
					.Select( cb => (RegexOptions)Enum.Parse( typeof( RegexOptions ), cb.Tag.ToString( ) ) )
					.Aggregate( RegexOptions.None, ( o, v ) => o | v );
		}


		internal void SetSelectedOptions( RegexOptions options )
		{
			try
			{
				++ChangeCounter;

				var cbs =
						pnl
							.Children
							.OfType<CheckBox>( )
							.Where( cb => RegexOptionsNames.Contains( cb.Tag?.ToString( ) ) );

				foreach( var cb in cbs )
				{
					cb.IsChecked = options.HasFlag( (RegexOptions)Enum.Parse( typeof( RegexOptions ), cb.Tag.ToString( ) ) );
				}

				CachedRegexOptions = options;
			}
			finally
			{
				--ChangeCounter;
			}
		}


		internal TimeSpan GetTimeout( )
		{
			return TimeSpan.Parse( ( (ComboBoxItem)cbxTimeout.SelectedItem ).Tag.ToString( ), CultureInfo.InvariantCulture );
		}


		internal void SetTimeout( TimeSpan timeout )
		{
			try
			{
				++ChangeCounter;

				CachedTimeout = timeout;

				var s = timeout.ToString( "c", CultureInfo.InvariantCulture );
				var item = cbxTimeout.Items.OfType<ComboBoxItem>( ).SingleOrDefault( i => s.Equals( i.Tag ) );
				if( item != null ) item.IsSelected = true;
			}
			finally
			{
				--ChangeCounter;
			}
		}


		private void UserControl_Loaded( object sender, RoutedEventArgs e )
		{
			if( IsFullyLoaded ) return;

			CachedRegexOptions = GetSelectedOptions( );
			CachedTimeout = GetTimeout( );

			IsFullyLoaded = true;
		}


		private void CbOption_CheckedChanged( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			CachedRegexOptions = GetSelectedOptions( );

			Changed?.Invoke( this, new RegexEngineOptionsChangedArgs { PreferImmediateReaction = false } );
		}


		private void cbxTimeout_SelectionChanged( object sender, SelectionChangedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			if( TimeSpan.TryParse( ( (ComboBoxItem)cbxTimeout.SelectedItem ).Tag.ToString( ), CultureInfo.InvariantCulture, out TimeSpan t ) )
			{
				CachedTimeout = t;
			}

			Changed?.Invoke( this, new RegexEngineOptionsChangedArgs { PreferImmediateReaction = true } );
		}
	}
}
