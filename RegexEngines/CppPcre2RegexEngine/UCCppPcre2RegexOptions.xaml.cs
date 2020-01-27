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

namespace CppPcre2RegexEngineNs
{
	/// <summary>
	/// Interaction logic for UCCppPcreRegexOptions.xaml
	/// </summary>
	public partial class UCCppPcre2RegexOptions : UserControl
	{
		internal event EventHandler Changed;
		internal string[] CachedOptions; // (accessible from threads)


		bool IsFullyLoaded = false;
		int ChangeCounter = 0;



		public class OptionInfo
		{
			public string Tag { get; set; }
			public string Text { get; set; }
		}


		public List<OptionInfo> OptionInfos { get; } = new List<OptionInfo>
		{
			new OptionInfo{Tag = "Tag1", Text = "Text1"},
			new OptionInfo{Tag = "Tag2", Text = "Text2"},
		} ;



		public UCCppPcre2RegexOptions( )
		{
			InitializeComponent( );

			DataContext = this;
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
			var cbs = pnl1.Children.OfType<CheckBox>( );

			return cbs
					.Where( cb => cb.IsChecked == true )
					.Select( cb => cb.Tag.ToString( ) )
					.ToArray( );
		}


		internal void SetSelectedOptions( string[] options )
		{
			try
			{
				++ChangeCounter;

				options = options ?? new string[] { };

				var cbs = pnl1.Children.OfType<CheckBox>( );

				foreach( var cb in cbs )
				{
					cb.IsChecked = options.Contains( cb.Tag.ToString( ) );
				}
			}
			finally
			{
				--ChangeCounter;
			}
		}



		private void CheckBox_Changed( object sender, RoutedEventArgs e )
		{

		}
	}
}
