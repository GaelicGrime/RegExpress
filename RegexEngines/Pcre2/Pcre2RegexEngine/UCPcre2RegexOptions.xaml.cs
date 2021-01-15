using RegexEngineInfrastructure;
using RegexEngineInfrastructure.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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


namespace Pcre2RegexEngineNs
{
	/// <summary>
	/// Interaction logic for UCCppPcreRegexOptions.xaml
	/// </summary>
	partial class UCPcre2RegexOptions : UserControl
	{
		internal event EventHandler<RegexEngineOptionsChangedArgs> Changed;
		internal string[] CachedOptions; // (accessible from threads)


		bool IsFullyLoaded = false;
		int ChangeCounter = 0;



		public UCPcre2RegexOptions( )
		{
			InitializeComponent( );

			// insert checkboxes

			{
				List<Pcre2RegexInterop.OptionInfo> compile_options = Pcre2RegexInterop.Matcher.GetCompileOptions( );

				foreach( var o in compile_options )
				{
					var cb = new CheckBox
					{
						Tag = o.FlagName,
						Content = new TextAndNote { Text = o.FlagName, Note = o.Note }
					};

					pnlCompileOptions.Children.Add( cb );
				}
			}

			{
				List<Pcre2RegexInterop.OptionInfo> extra_compile_options = Pcre2RegexInterop.Matcher.GetExtraCompileOptions( );

				foreach( var o in extra_compile_options )
				{
					var cb = new CheckBox
					{
						Tag = o.FlagName,
						Content = new TextAndNote { Text = o.FlagName, Note = o.Note }
					};

					pnlExtraCompileOptions.Children.Add( cb );
				}
			}

			{
				List<Pcre2RegexInterop.OptionInfo> match_options = Pcre2RegexInterop.Matcher.GetMatchOptions( );

				foreach( var o in match_options )
				{
					var cb = new CheckBox
					{
						Tag = o.FlagName,
						Content = new TextAndNote { Text = o.FlagName, Note = o.Note }
					};

					pnlMatchOptions.Children.Add( cb );
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
			// TODO: add support for 'pcre2_set_bsr'?
			// TODO: add support for 'pcre2_set_newline'?

			return
				( new[] { ( (ComboBoxItem)cbxAlgorithm.SelectedItem )?.Tag.ToString( ) ?? "Standard" } )
				.Concat(
				pnlCompileOptions.Children.OfType<CheckBox>( )
					.Where( cb => cb.IsChecked == true )
					.Select( cb => "c:" + cb.Tag )
				)
				.Concat(
				pnlExtraCompileOptions.Children.OfType<CheckBox>( )
					.Where( cb => cb.IsChecked == true )
					.Select( cb => "x:" + cb.Tag )
				)
				.Concat(
				pnlMatchOptions.Children.OfType<CheckBox>( )
					.Where( cb => cb.IsChecked == true )
					.Select( cb => "m:" + cb.Tag )
				)
				.ToArray( );
		}


		internal void SetSelectedOptions( string[] options )
		{
			try
			{
				++ChangeCounter;

				options = options ?? new string[] { };

				var a = cbxAlgorithm.Items.Cast<ComboBoxItem>( ).FirstOrDefault( i => options.Contains( i.Tag.ToString( ) ) );
				if( a == null ) a = cbxAlgorithm.Items.Cast<ComboBoxItem>( ).FirstOrDefault( i => i.Tag.ToString( ) == "Standard" );
				cbxAlgorithm.SelectedItem = a;

				foreach( var cb in pnlCompileOptions.Children.OfType<CheckBox>( ) )
				{
					cb.IsChecked = options.Contains( "c:" + cb.Tag );
				}

				foreach( var cb in pnlExtraCompileOptions.Children.OfType<CheckBox>( ) )
				{
					cb.IsChecked = options.Contains( "x:" + cb.Tag );
				}

				foreach( var cb in pnlMatchOptions.Children.OfType<CheckBox>( ) )
				{
					cb.IsChecked = options.Contains( "m:" + cb.Tag );
				}
			}
			finally
			{
				--ChangeCounter;
			}
		}


		internal bool IsCompileOptionSelected( string tag )
		{
			return CachedOptions.Contains( "c:" + tag );
		}


		internal bool IsExtraCompileOptionSelected( string tag )
		{
			return CachedOptions.Contains( "x:" + tag );
		}



		private void UserControl_Loaded( object sender, RoutedEventArgs e )
		{
			if( IsFullyLoaded ) return;

			CachedOptions = GetSelectedOptions( );

			IsFullyLoaded = true;
		}



		private void cbxAlgorithm_SelectionChanged( object sender, SelectionChangedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			CachedOptions = GetSelectedOptions( );

			Changed?.Invoke( null, new RegexEngineOptionsChangedArgs { PreferImmediateReaction = true } );
		}


		private void CheckBox_Changed( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			CachedOptions = GetSelectedOptions( );

			Changed?.Invoke( null, new RegexEngineOptionsChangedArgs { PreferImmediateReaction = false } );
		}

	}
}
