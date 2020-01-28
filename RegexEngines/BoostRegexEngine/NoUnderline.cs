using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Markup;


namespace BoostRegexEngineNs
{
	public class NoUnderline : MarkupExtension, IValueConverter
	{
		public override object ProvideValue( IServiceProvider serviceProvider )
		{
			return this;
		}


		#region IValueConverter

		public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
		{
			return value.ToString( ).Replace( "_", "__" );
		}


		public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
		{
			return value.ToString( ).Replace( "__", "_" );
		}

		#endregion
	}
}
