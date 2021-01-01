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
		internal string[] CachedOptions; // (accessible from threads)


		bool IsFullyLoaded = false;
		int ChangeCounter = 0;


		public UCIcuRegexOptions( )
		{
			InitializeComponent( );


			// insert checkboxes

			{
				List<IcuRegexInterop.OptionInfo> compile_options = IcuRegexInterop.Matcher.GetOptions( );

				foreach( var o in compile_options )
				{
					var cb = new CheckBox
					{
						Tag = o.FlagName,
						Content = CreateTextBlock( o.FlagName, o.Note )
					};

					pnlOptions.Children.Add( cb );
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
			var selected_options =
				pnlOptions.Children.OfType<CheckBox>( )
					.Where( cb => cb.IsChecked == true )
					.Select( cb => cb.Tag.ToString( ) );

			var limit = tbxIterationLimit.Text.Trim( );

			if( !string.IsNullOrWhiteSpace( limit ) && limit != "0" )
				selected_options = selected_options.Append( "limit:" + limit );

			return selected_options.ToArray( );
		}


		internal void SetSelectedOptions( string[] options )
		{
			try
			{
				++ChangeCounter;

				options = options ?? new string[] { };

				foreach( var cb in pnlOptions.Children.OfType<CheckBox>( ) )
				{
					cb.IsChecked = options.Contains( cb.Tag );
				}

				var limit = options.FirstOrDefault( o => o.StartsWith( "limit:" ) );
				if( limit != null ) limit = limit.Substring( "limit:".Length );
				if( string.IsNullOrWhiteSpace( limit ) ) limit = "0";
				tbxIterationLimit.Text = limit;
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
		}


		private void CheckBox_Changed( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			CachedOptions = GetSelectedOptions( );

			Changed?.Invoke( null, new RegexEngineOptionsChangedArgs { PreferImmediateReaction = false } );
		}


		private void tbxIterationLimit_TextChanged( object sender, TextChangedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			CachedOptions = GetSelectedOptions( );

			Changed?.Invoke( null, new RegexEngineOptionsChangedArgs { PreferImmediateReaction = false } );
		}


		static TextBlock CreateTextBlock( string text, string note )
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
