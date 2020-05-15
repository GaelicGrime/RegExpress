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


namespace StdRegexEngineNs
{
	/// <summary>
	/// Interaction logic for UCStdRegexOptions.xaml
	/// </summary>
	public partial class UCStdRegexOptions : UserControl
	{
		internal event EventHandler<RegexEngineOptionsChangedArgs> Changed;
		internal string[] CachedOptions; // (accessible from threads)


		bool IsFullyLoaded = false;
		int ChangeCounter = 0;


		public UCStdRegexOptions( )
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


		string[] GetSelectedOptions( ) // (not accessible from threads; use 'CachedOptions')
		{
			var cbs = pnl1.Children.OfType<CheckBox>( ).Concat( pnl2.Children.OfType<CheckBox>( ) );

			return cbs
					.Where( cb => cb.IsChecked == true )
					.Select( cb => cb.Tag.ToString( ) )
					.Append( ( (ComboBoxItem)cbxGrammar.SelectedItem ).Tag.ToString( ) )
					.Append( StdRegexInterop.Matcher.OptionPrefix_REGEX_MAX_STACK_COUNT + tbREGEX_MAX_STACK_COUNT.Text )
					.Append( StdRegexInterop.Matcher.OptionPrefix_REGEX_MAX_COMPLEXITY_COUNT + tbREGEX_MAX_COMPLEXITY_COUNT.Text )
					.ToArray( );
		}


		void SetSelectedOptions( string[] options )
		{
			try
			{
				++ChangeCounter;

				options = options ?? new string[] { };

				var g = cbxGrammar.Items.Cast<ComboBoxItem>( ).FirstOrDefault( i => options.Contains( i.Tag.ToString( ) ) );
				cbxGrammar.SelectedItem = g;

				var cbs = pnl1.Children.OfType<CheckBox>( ).Concat( pnl2.Children.OfType<CheckBox>( ) );

				foreach( var cb in cbs )
				{
					cb.IsChecked = options.Contains( cb.Tag.ToString( ) );
				}

				var msc = options.FirstOrDefault( o => o.StartsWith( StdRegexInterop.Matcher.OptionPrefix_REGEX_MAX_STACK_COUNT ) );
				if( msc == null )
				{
					tbREGEX_MAX_STACK_COUNT.Text = "600";
				}
				else
				{
					tbREGEX_MAX_STACK_COUNT.Text = msc.Substring( StdRegexInterop.Matcher.OptionPrefix_REGEX_MAX_STACK_COUNT.Length );
				}

				var mcc = options.FirstOrDefault( o => o.StartsWith( StdRegexInterop.Matcher.OptionPrefix_REGEX_MAX_COMPLEXITY_COUNT ) );
				if( mcc == null )
				{
					tbREGEX_MAX_COMPLEXITY_COUNT.Text = "10000000";
				}
				else
				{
					tbREGEX_MAX_COMPLEXITY_COUNT.Text = mcc.Substring( StdRegexInterop.Matcher.OptionPrefix_REGEX_MAX_COMPLEXITY_COUNT.Length );
				}

			}
			finally
			{
				--ChangeCounter;
			}
		}


		internal GrammarEnum GetGrammar( ) // (accessible from threads)
		{
			string grammar_s = Enum.GetNames( typeof( GrammarEnum ) ).FirstOrDefault( n => n != "None" && CachedOptions.Contains( n ) );
			if( grammar_s == null ) return GrammarEnum.None;

			return (GrammarEnum)Enum.Parse( typeof( GrammarEnum ), grammar_s );
		}


		private void UserControl_Loaded( object sender, RoutedEventArgs e )
		{
			if( IsFullyLoaded ) return;

			CachedOptions = GetSelectedOptions( );

			IsFullyLoaded = true;
		}


		private void cbxGrammar_SelectionChanged( object sender, SelectionChangedEventArgs e )
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

		private void tbREGEX_MAX_STACK_COUNT_TextChanged( object sender, TextChangedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			CachedOptions = GetSelectedOptions( );

			Changed?.Invoke( null, new RegexEngineOptionsChangedArgs { PreferImmediateReaction = false } );
		}
	}
}
