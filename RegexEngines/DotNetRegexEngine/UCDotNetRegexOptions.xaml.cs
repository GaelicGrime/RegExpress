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


namespace DotNetRegexEngineNs
{
	/// <summary>
	/// Interaction logic for UCDotNetRegexOptions.xaml
	/// </summary>
	public partial class UCDotNetRegexOptions : UserControl
	{
		internal event EventHandler Changed;

		internal RegexOptions CachedRegexOptions; // (accessible from threads)


		public UCDotNetRegexOptions( )
		{
			InitializeComponent( );
		}


		internal object ToSerialisableObject( )
		{
			return pnl
				.Children
				.OfType<CheckBox>( )
				.Where( cb => cb.IsChecked == true )
				.Select( cb => cb.Tag as RegexOptions? )
				.Where( v => v != null )
				.Select( v => v.Value.ToString( ) )
				.ToArray( );
		}


		internal void FromSerializableObject( object obj )
		{
			RegexOptions options = RegexOptions.None;

			switch( obj )
			{
			case object[] arr:
				options = Enum.GetNames( typeof( RegexOptions ) )
					.Intersect( arr.OfType<string>( ), StringComparer.InvariantCultureIgnoreCase )
					.Select( s => (RegexOptions)Enum.Parse( typeof( RegexOptions ), s, ignoreCase: true ) )
					.Aggregate( RegexOptions.None, ( a, o ) => a | o );
				break;
			case int i: // previous version
				options = (RegexOptions)( i &
					(int)Enum.GetValues( typeof( RegexOptions ) )
						.Cast<RegexOptions>( )
						.Aggregate( RegexOptions.None, ( a, v ) => a | v ) );
				break;
			}

			SetSelectedOptions( options );
		}


		internal RegexOptions GetSelectedOptions( )
		{
			return
				pnl
					.Children
					.OfType<CheckBox>( )
					.Where( cb => cb.IsChecked == true )
					.Select( cb => cb.Tag as RegexOptions? )
					.Where( v => v != null )
					.Select( v => v.Value )
					.Aggregate( RegexOptions.None, ( o, v ) => o | v );
		}


		internal void SetSelectedOptions( RegexOptions options )
		{
			EnsureControls( );

			var cbs =
					pnl
						.Children
						.OfType<CheckBox>( )
						.Where( cb => cb.Tag is RegexOptions? );

			foreach( var cb in cbs )
			{
				cb.IsChecked = options.HasFlag( ( (RegexOptions?)cb.Tag ).Value );
			}

			CachedRegexOptions = options;
		}


		private void UserControl_Loaded( object sender, RoutedEventArgs e )
		{
			EnsureControls( );
		}


		private void CbOption_CheckedChanged( object sender, RoutedEventArgs e )
		{
			CachedRegexOptions = GetSelectedOptions( );

			Changed?.Invoke( sender, e );
		}


		void EnsureControls( )
		{
			if( pnl.Children.Count == 0 )
			{
				MakeOptionCheckbox( RegexOptions.CultureInvariant );
				MakeOptionCheckbox( RegexOptions.ECMAScript );
				MakeOptionCheckbox( RegexOptions.ExplicitCapture );
				MakeOptionCheckbox( RegexOptions.IgnoreCase );
				MakeOptionCheckbox( RegexOptions.IgnorePatternWhitespace );
				MakeOptionCheckbox( RegexOptions.Multiline, "('^', '$' at '\\n' too)" );
				MakeOptionCheckbox( RegexOptions.RightToLeft );
				MakeOptionCheckbox( RegexOptions.Singleline, "('.' matches '\\n' too)" );
			}
		}

		void MakeOptionCheckbox( RegexOptions option, string note = null )
		{
			var text = option.ToString( );
			if( note != null ) text += ' ' + note;

			var cb = new CheckBox
			{
				Content = text,
				Tag = (RegexOptions?)option
			};

			cb.Checked += CbOption_CheckedChanged;
			cb.Unchecked += CbOption_CheckedChanged;

			pnl.Children.Add( cb );
		}
	}
}
