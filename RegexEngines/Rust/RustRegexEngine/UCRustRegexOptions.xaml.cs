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
		internal RustRegexOptions CachedOptions; // (accessible from threads)


		bool IsFullyLoaded = false;
		int ChangeCounter = 0;


		public UCRustRegexOptions( )
		{
			InitializeComponent( );
		}


		internal RustRegexOptions ExportOptions( )
		{
			return GetSelectedOptions( );
		}


		internal void ImportOptions( RustRegexOptions options )
		{
			SetSelectedOptions( options );
		}


		internal RustRegexOptions GetSelectedOptions( )
		{
			var options = new RustRegexOptions( );

			options.@struct = ( (ComboBoxItem)cbxStruct.SelectedItem )?.Tag?.ToString( );

			options.case_insensitive = chb_case_insensitive.IsChecked == true;
			options.multi_line = chb_multi_line.IsChecked == true;
			options.dot_matches_new_line = chb_dot_matches_new_line.IsChecked == true;
			options.swap_greed = chb_swap_greed.IsChecked == true;
			options.ignore_whitespace = chb_ignore_whitespace.IsChecked == true;
			options.unicode = chb_unicode.IsChecked == true;
			options.octal = chb_octal.IsChecked == true;

			options.size_limit = tbx_size_limit.Text;
			options.dfa_size_limit = tbx_dfa_size_limit.Text;
			options.nest_limit = tbx_nest_limit.Text;

			return options;
		}

		internal void SetSelectedOptions( RustRegexOptions options )
		{
			try
			{
				++ChangeCounter;

				options = options ?? new RustRegexOptions( );

				if( options.@struct == "RegexBuilder" )
					cbiRegexBuilder.IsSelected = true;
				else
					cbiRegex.IsSelected = true;

				chb_case_insensitive.IsChecked = options.case_insensitive;
				chb_multi_line.IsChecked = options.multi_line;
				chb_dot_matches_new_line.IsChecked = options.dot_matches_new_line;
				chb_swap_greed.IsChecked = options.swap_greed;
				chb_ignore_whitespace.IsChecked = options.ignore_whitespace;
				chb_unicode.IsChecked = options.unicode;
				chb_octal.IsChecked = options.octal;

				tbx_size_limit.Text = options.size_limit;
				tbx_dfa_size_limit.Text = options.dfa_size_limit;
				tbx_nest_limit.Text = options.nest_limit;

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
