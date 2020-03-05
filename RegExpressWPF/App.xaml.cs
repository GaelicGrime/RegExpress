using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Threading.Tasks;
using System.Windows;


namespace RegExpressWPF
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public App( )
		{
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			//Dispatcher.UnhandledException += Dispatcher_UnhandledException;
			//DispatcherUnhandledException += App_DispatcherUnhandledException;
			//TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
		}

		//private void TaskScheduler_UnobservedTaskException( object sender, UnobservedTaskExceptionEventArgs e )
		//{
		//}

		//private void App_DispatcherUnhandledException( object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e )
		//{
		//}

		//private void Dispatcher_UnhandledException( object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e )
		//{
		//}


		//[ReliabilityContract(...)]
		private void CurrentDomain_UnhandledException( object sender, UnhandledExceptionEventArgs e )
		{
			string m;

			const int LINES_TO_SHOW = 10;

			switch( e.ExceptionObject )
			{
			case Exception exc:
				m = string.Join( Environment.NewLine, exc.ToString( ).Split( new[] { "\r\n", "\r", "\n" }, LINES_TO_SHOW + 1, StringSplitOptions.None ).Take( LINES_TO_SHOW ) );
				break;
			case null:
				m = "";
				break;
			case object obj:
				m = obj.GetType( ).FullName;
				break;
			}

			MessageBox.Show(
				"Unhandled exception has occured." + Environment.NewLine + Environment.NewLine + m,
				"RegExpress Error",
				MessageBoxButton.OK,
				MessageBoxImage.Error

				);
		}
	}
}
