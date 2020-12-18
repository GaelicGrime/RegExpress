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
	public partial class UCRustRegexOptions : UserControl
	{
		internal event EventHandler<RegexEngineOptionsChangedArgs> Changed;
		internal string[] CachedOptions; // (accessible from threads)


		bool IsFullyLoaded = false;
		int ChangeCounter = 0;


		public UCRustRegexOptions( )
		{
			InitializeComponent( );
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
			var options = new List<string>( );

			options.Add( "struct:" + ( (ComboBoxItem)cbxStruct.SelectedItem ).Tag );

			if( chb_case_insensitive.IsChecked == true ) options.Add( "case_insensitive" );
			if( chb_multi_line.IsChecked == true ) options.Add( "multi_line" );
			if( chb_dot_matches_new_line.IsChecked == true ) options.Add( "dot_matches_new_line" );
			if( chb_swap_greed.IsChecked == true ) options.Add( "swap_greed" );
			if( chb_ignore_whitespace.IsChecked == true ) options.Add( "ignore_whitespace" );
			if( chb_unicode.IsChecked == true ) options.Add( "unicode" );
			if( chb_octal.IsChecked == true ) options.Add( "octal" );

			options.Add( "size_limit:" + tbx_size_limit.Text );
			options.Add( "dfa_size_limit:" + tbx_dfa_size_limit.Text );
			options.Add( "nest_limit:" + tbx_nest_limit.Text );

			return options.ToArray( );
		}

		internal void SetSelectedOptions( string[] options )
		{
			try
			{
				++ChangeCounter;

				options = options ?? new string[] { };

				if( options.Any( o => Regex.IsMatch( o, @"struct:\s*RegexBuilder" ) ) ) cbiRegexBuilder.IsSelected = true;
				else cbiRegex.IsSelected = true;

				chb_case_insensitive.IsChecked = options.Contains( "case_insensitive" );
				chb_multi_line.IsChecked = options.Contains( "multi_line" );
				chb_dot_matches_new_line.IsChecked = options.Contains( "dot_matches_new_line" );
				chb_swap_greed.IsChecked = options.Contains( "swap_greed" );
				chb_ignore_whitespace.IsChecked = options.Contains( "ignore_whitespace" );
				chb_unicode.IsChecked = options.Contains( "unicode" );
				chb_octal.IsChecked = options.Contains( "octal" );

				tbx_size_limit.Text = options.Select( o => Regex.Match( o, @"size_limit:(.*)" ) ).FirstOrDefault( m => m.Success )?.Groups[1].Value.Trim( ) ?? "0";
				tbx_dfa_size_limit.Text = options.Select( o => Regex.Match( o, @"dfa_size_limit:(.*)" ) ).FirstOrDefault( m => m.Success )?.Groups[1].Value.Trim( ) ?? "0";
				tbx_nest_limit.Text = options.Select( o => Regex.Match( o, @"nest_limit:(.*)" ) ).FirstOrDefault( m => m.Success )?.Groups[1].Value.Trim( ) ?? "0";

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

			CachedOptions = GetSelectedOptions( );

			IsFullyLoaded = true;

			UpdateControls( );
		}


		private void CheckBox_Changed( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			CachedOptions = GetSelectedOptions( );

			Changed?.Invoke( null, new RegexEngineOptionsChangedArgs { PreferImmediateReaction = false } );
		}


		private void cbxStruct_SelectionChanged( object sender, SelectionChangedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			CachedOptions = GetSelectedOptions( );

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

				chb_case_insensitive.IsEnabled =
					chb_multi_line.IsEnabled =
					chb_dot_matches_new_line.IsEnabled =
					chb_swap_greed.IsEnabled =
					chb_ignore_whitespace.IsEnabled =
					chb_unicode.IsEnabled =
					chb_octal.IsEnabled =
					tbx_size_limit.IsEnabled =
					tbx_dfa_size_limit.IsEnabled =
					tbx_nest_limit.IsEnabled = is_builder;
			}
			finally
			{
				--ChangeCounter;
			}

		}
	}
}
