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


namespace RegexEngineInfrastructure.UI
{
	/// <summary>
	/// Interaction logic for TextAndNote.xaml
	/// </summary>
	public partial class TextAndNote : UserControl
	{
		public TextAndNote( )
		{
			InitializeComponent( );

			sep.Text = "";
			note.Text = null;
		}


		public string Text
		{
			get => txt.Text;
			set => txt.Text = value;
		}

		public string Note
		{
			get => note.Text;
			set
			{
				sep.Text = string.IsNullOrWhiteSpace( value ) ? null : " – ";
				note.Text = value;
			}
		}
	}
}
