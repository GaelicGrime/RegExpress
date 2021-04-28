using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;


namespace RegExpressWPF
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		[DllImport( "user32" )]
		static extern bool AllowSetForegroundWindow( Int32 pid );

		[DllImport( "user32" )]
		static extern bool SetForegroundWindow( IntPtr hWnd );


		const string SingleInstanceMutexName = "RegExpress-SingleInstance-Mutex-1";
		const string SingleInstanceEventName = "RegExpress-SingleInstance-Event-1";

		Mutex mSingleInstanceMutex = null;
		EventWaitHandle mSingleInstanceEvent;


		public App( )
		{
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			//Dispatcher.UnhandledException += Dispatcher_UnhandledException;
			//DispatcherUnhandledException += App_DispatcherUnhandledException;
			//TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
		}


		private void App_Startup( object sender, StartupEventArgs e )
		{
			Debug.Assert( mSingleInstanceMutex == null );

			bool new_mutex_created;
			mSingleInstanceMutex = new Mutex( false, SingleInstanceMutexName, out new_mutex_created );

			if( new_mutex_created )
			{
				bool new_event_created;
				mSingleInstanceEvent = new EventWaitHandle( false, EventResetMode.AutoReset, SingleInstanceEventName, out new_event_created );

				Debug.Assert( new_event_created );

				var thread = new Thread( SingleInstanceThreadProc ) { IsBackground = true };
				thread.Start( );
			}
			else
			{
				var current_process = Process.GetCurrentProcess( );
				var other_process = Process.GetProcessesByName( current_process.ProcessName ).FirstOrDefault( p => p.Id != current_process.Id );
				Debug.Assert( other_process != null );

				SetForegroundWindow( other_process.MainWindowHandle );

				if( other_process != null )
				{
					AllowSetForegroundWindow( other_process.Id );
				}

				mSingleInstanceEvent = new EventWaitHandle( false, EventResetMode.AutoReset, SingleInstanceEventName );
				mSingleInstanceEvent.Set( );

				Shutdown( );
			}
		}


		private void SingleInstanceThreadProc( )
		{
			for(; ; )
			{
				mSingleInstanceEvent.WaitOne( );

				Dispatcher.BeginInvoke( DispatcherPriority.Normal,
					new Action( ( ) =>
					{
						try
						{
							var current_process = Process.GetCurrentProcess( );
							var current_main_hwnd = current_process.MainWindowHandle;

							SetForegroundWindow( current_main_hwnd );
							//MainWindow.Focus( );
						}
						catch( Exception exc )
						{
							_ = exc;
							if( Debugger.IsAttached ) Debugger.Break( );

							// ignore
						}
					} ) );
			}
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
