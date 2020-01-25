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
		internal event EventHandler Changed;

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


		internal object ToSerialisableObject( )
		{
			return pnl
				.Children
				.OfType<CheckBox>( )
				.Where( cb => cb.IsChecked == true && RegexOptionsNames.Contains( cb.Tag?.ToString( ) ) )
				.Select( cb => cb.Tag )
				.Concat( new[] { $"timeout:{CachedTimeout.ToString( "c", CultureInfo.InvariantCulture )}" } )
				.ToArray( );
		}


		internal void FromSerializableObject( object obj )
		{
			RegexOptions options = RegexOptions.None;
			TimeSpan timeout = TimeSpan.FromSeconds( 10 );

			switch( obj )
			{
			case int i: // previous version
				options = (RegexOptions)( i &
					(int)Enum.GetValues( typeof( RegexOptions ) )
						.Cast<RegexOptions>( )
						.Aggregate( RegexOptions.None, ( a, v ) => a | v ) );
				break;
			case object[] arr:
				options = Enum.GetNames( typeof( RegexOptions ) )
					.Intersect( arr.OfType<string>( ), StringComparer.InvariantCultureIgnoreCase )
					.Select( s => (RegexOptions)Enum.Parse( typeof( RegexOptions ), s, ignoreCase: true ) )
					.Aggregate( RegexOptions.None, ( a, o ) => a | o );

				string timeout_s =
					arr
					.OfType<string>( )
					.FirstOrDefault( s => s.StartsWith( "timeout:" ) )
					?.Substring( "timeout:".Length );

				if( TimeSpan.TryParse( timeout_s, CultureInfo.InvariantCulture, out TimeSpan t ) ) timeout = t;
				break;
			}

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

			Changed?.Invoke( sender, e );
		}


		private void cbxTimeout_SelectionChanged( object sender, SelectionChangedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			if( TimeSpan.TryParse( ( (ComboBoxItem)cbxTimeout.SelectedItem ).Tag.ToString( ), CultureInfo.InvariantCulture, out TimeSpan t ) )
			{
				CachedTimeout = t;
			}

			Changed?.Invoke( sender, e );
		}
	}
}
