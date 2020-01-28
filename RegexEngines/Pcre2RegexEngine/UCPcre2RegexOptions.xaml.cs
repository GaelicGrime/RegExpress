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
	public partial class UCPcre2RegexOptions : UserControl
	{
		internal event EventHandler Changed;
		internal string[] CachedOptions; // (accessible from threads)


		bool IsFullyLoaded = false;
		int ChangeCounter = 0;



		public UCPcre2RegexOptions( )
		{
			InitializeComponent( );

			{
				List<Pcre2RegexInterop.OptionInfo> compile_options = Pcre2RegexInterop.Matcher.GetCompileOptions( );

				foreach( var o in compile_options )
				{
					var cb = new CheckBox
					{
						Tag = o.FlagName,
						Content = ( o.FlagName + " – " + o.Note ).Replace( "_", "__" )
					};

					pnlCompileOptions.Children.Add( cb );
				}
			}

			{
				List<Pcre2RegexInterop.OptionInfo> match_options = Pcre2RegexInterop.Matcher.GetMatchOptions( );

				foreach( var o in match_options )
				{
					var cb = new CheckBox
					{
						Tag = o.FlagName,
						Content = ( o.FlagName + " – " + o.Note ).Replace( "_", "__" )
					};

					pnlMatchOptions.Children.Add( cb );
				}

			}
		}


		internal object ToSerialisableObject( )
		{
			return GetSelectedOptions( );
		}


		internal void FromSerializableObject( object obj )
		{
			string[] arr = obj as string[];

			if( arr == null )
			{
				if( obj is object[] ) arr = ( (object[])obj ).OfType<string>( ).ToArray( );
			}

			SetSelectedOptions( arr );
		}


		internal string[] GetSelectedOptions( )
		{
			return pnlCompileOptions.Children.OfType<CheckBox>( )
					.Where( cb => cb.IsChecked == true )
					.Select( cb => "c:" + cb.Tag.ToString( ) )
					.Concat(
					pnlMatchOptions.Children.OfType<CheckBox>( )
					.Where( cb => cb.IsChecked == true )
					.Select( cb => "m:" + cb.Tag.ToString( ) )
					)
					.ToArray( );
		}


		internal void SetSelectedOptions( string[] options )
		{
			try
			{
				++ChangeCounter;

				options = options ?? new string[] { };

				foreach( var cb in pnlCompileOptions.Children.OfType<CheckBox>( ) )
				{
					cb.IsChecked = options.Contains( "c:" + cb.Tag );
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

			Changed?.Invoke( null, null );
		}
	}
}
