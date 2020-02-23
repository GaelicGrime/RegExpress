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


namespace OnigurumaRegexEngineNs
{
	/// <summary>
	/// Interaction logic for UCOnigurumaRegexOptions.xaml
	/// </summary>
	public partial class UCOnigurumaRegexOptions : UserControl
	{
		internal event EventHandler<RegexEngineOptionsChangedArgs> Changed;
		internal string[] CachedOptions; // (accessible from threads)


		bool IsFullyLoaded = false;
		int ChangeCounter = 0;


		public UCOnigurumaRegexOptions( )
		{
			InitializeComponent( );


			// insert syntaxes

			{
				List<OnigurumaRegexInterop.OptionInfo> syntax_options = OnigurumaRegexInterop.Matcher.GetSyntaxOptions( );

				foreach( var o in syntax_options )
				{
					var cbi = new ComboBoxItem
					{
						Tag = o.FlagName,
						Content = CreateTextBlock( o.FlagName, o.Note )
					};

					cbxSyntax.Items.Add( cbi );
				}

				// select first (default)
				cbxSyntax.SelectedItem = cbxSyntax.Items.OfType<ComboBoxItem>( ).FirstOrDefault( );
			}

			// insert checkboxes

			{
				List<OnigurumaRegexInterop.OptionInfo> compile_options = OnigurumaRegexInterop.Matcher.GetCompileOptions( );

				foreach( var o in compile_options )
				{
					var cb = new CheckBox
					{
						Tag = o.FlagName,
						Content = CreateTextBlock( o.FlagName, o.Note )
					};

					pnlCompileOptions.Children.Add( cb );
				}
			}

			{
				List<OnigurumaRegexInterop.OptionInfo> compile_options = OnigurumaRegexInterop.Matcher.GetSearchOptions( );

				foreach( var o in compile_options )
				{
					var cb = new CheckBox
					{
						Tag = o.FlagName,
						Content = CreateTextBlock( o.FlagName, o.Note )
					};

					pnlSearchOptions.Children.Add( cb );
				}
			}
			{
				List<OnigurumaRegexInterop.OptionInfo> configuration_options = OnigurumaRegexInterop.Matcher.GetConfigurationOptions( );

				foreach( var o in configuration_options )
				{
					var cb = new CheckBox
					{
						Tag = o.FlagName,
						Content = CreateTextBlock( o.FlagName, o.Note )
					};

					pnlConfigurationOptions.Children.Add( cb );
				}
			}

		}


		internal string[] ExportOptions( )
		{
			return GetSelectedOptions( );
		}


		internal void ImportOptions( string[] options )
		{
			SetSelectedOptions( options );
		}


		internal string[] GetSelectedOptions( )
		{
			var syntax = ( cbxSyntax.SelectedItem as ComboBoxItem )?.Tag?.ToString( );
			if( syntax == null )
			{
				// get first (default)
				syntax = cbxSyntax.Items.OfType<ComboBoxItem>( ).FirstOrDefault( )?.Tag?.ToString( );
			}

			var compile_options =
				pnlCompileOptions.Children.OfType<CheckBox>( )
					.Where( cb => cb.IsChecked == true )
					.Select( cb => cb.Tag.ToString( ) );

			var search_options =
				pnlSearchOptions.Children.OfType<CheckBox>( )
					.Where( cb => cb.IsChecked == true )
					.Select( cb => cb.Tag.ToString( ) );

			var configuration_options =
				pnlConfigurationOptions.Children.OfType<CheckBox>( )
					.Where( cb => cb.IsChecked == true )
					.Select( cb => cb.Tag.ToString( ) );

			return
				new[] { syntax }.Concat( compile_options ).Concat( search_options ).Concat( configuration_options ).ToArray( );
		}


		internal void SetSelectedOptions( string[] options )
		{
			try
			{
				++ChangeCounter;

				options = options ?? new string[] { };

				var syntax_item = cbxSyntax.Items.OfType<ComboBoxItem>( ).FirstOrDefault( i => options.Contains( i.Tag.ToString( ) ) );
				if( syntax_item == null )
				{
					// get first (default)
					syntax_item = cbxSyntax.Items.OfType<ComboBoxItem>( ).FirstOrDefault( );
				}
				cbxSyntax.SelectedItem = syntax_item;

				foreach( var cb in pnlCompileOptions.Children.OfType<CheckBox>( ) )
				{
					cb.IsChecked = options.Contains( cb.Tag.ToString( ) );
				}

				foreach( var cb in pnlSearchOptions.Children.OfType<CheckBox>( ) )
				{
					cb.IsChecked = options.Contains( cb.Tag.ToString( ) );
				}

				foreach( var cb in pnlConfigurationOptions.Children.OfType<CheckBox>( ) )
				{
					cb.IsChecked = options.Contains( cb.Tag.ToString( ) );
				}
			}
			finally
			{
				--ChangeCounter;
			}
		}


		internal OnigurumaRegexInterop.OnigurumaHelper CreateOnigurumaHelper( )
		{
			return OnigurumaRegexInterop.Matcher.CreateOnigurumaHelper( CachedOptions );
		}


		internal string GetSyntax( ) //................
		{
			List<OnigurumaRegexInterop.OptionInfo> syntax_options = OnigurumaRegexInterop.Matcher.GetSyntaxOptions( );

			return CachedOptions.FirstOrDefault( o => syntax_options.Any( i => i.FlagName == o ) );
		}


		internal bool IsOptionSelected( string tag ) //................
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

			Changed?.Invoke( null, new RegexEngineOptionsChangedArgs { PreferImmediateReaction = false } );
		}

		private void cbxSyntax_SelectionChanged( object sender, SelectionChangedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			CachedOptions = GetSelectedOptions( );

			Changed?.Invoke( null, new RegexEngineOptionsChangedArgs { PreferImmediateReaction = true } );
		}


		TextBlock CreateTextBlock( string text, string note )
		{
			var tb = new TextBlock( );
			new Run( text, tb.ContentEnd );
			if( !string.IsNullOrWhiteSpace( note ) )
			{
				new Run( " – " + note, tb.ContentEnd )
					.SetValue( Run.ForegroundProperty, new SolidColorBrush { Opacity = 0.77, Color = SystemColors.ControlTextColor } );
			}

			return tb;
		}

	}
}
