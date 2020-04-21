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


namespace Perl5RegexEngineNs
{
	/// <summary>
	/// Interaction logic for UCPerl5RegexOptions.xaml
	/// </summary>
	public partial class UCPerl5RegexOptions : UserControl
	{
		internal event EventHandler<RegexEngineOptionsChangedArgs> Changed;
		internal string[] CachedOptions; // (accessible from threads)


		bool IsFullyLoaded = false;
		int ChangeCounter = 0;


		public UCPerl5RegexOptions( )
		{
			InitializeComponent( );
		}

		internal string[] ExportOptions( )
		{
			return GetSelectedOptions( );
		}


		internal void ImportOptions(string[] options)
		{
			SetSelectedOptions( options );
		}


		internal string[] GetSelectedOptions( )
		{
			return null;
		}

		internal void SetSelectedOptions(string[] options)
		{
			try
			{
				++ChangeCounter;

				//...
			}
			finally
			{
				--ChangeCounter;
			}
		}

	}
}
