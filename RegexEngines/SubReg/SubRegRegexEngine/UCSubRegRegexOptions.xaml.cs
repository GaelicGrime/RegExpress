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

namespace SubRegRegexEngineNs
{
	/// <summary>
	/// Interaction logic for UCSubRegRegexOptions.xaml
	/// </summary>
	partial class UCSubRegRegexOptions : UserControl
	{
		internal event EventHandler<RegexEngineOptionsChangedArgs> Changed;
		internal string[] CachedOptions; // (accessible from threads)


		bool IsFullyLoaded = false;
		int ChangeCounter = 0;


		public UCSubRegRegexOptions( )
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
			var maximum_depth = tbxMaximumDepth.Text.Trim( );

			return new string[] { "depth:" + maximum_depth };
		}


		internal void SetSelectedOptions( string[] options )
		{
			try
			{
				++ChangeCounter;

				options = options ?? new string[] { };

				var maximum_depth = options.FirstOrDefault( o => o.StartsWith( "depth:" ) );
				if( maximum_depth == null )
				{
					maximum_depth = "4";
				}
				else
				{
					maximum_depth = maximum_depth.Substring( "depth:".Length );
				}
				tbxMaximumDepth.Text = maximum_depth;
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


		private void tbxMaximumDepth_TextChanged( object sender, TextChangedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			CachedOptions = GetSelectedOptions( );

			Changed?.Invoke( null, new RegexEngineOptionsChangedArgs { PreferImmediateReaction = false } );
		}
	}
}
