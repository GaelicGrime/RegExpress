using RegexEngineInfrastructure;
using RegExpressWPF.Code;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;


namespace RegExpressWPF
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, IDisposable
	{
		readonly ResumableLoop AutoSaveLoop;

		bool IsFullyLoaded = false;

		public static readonly RoutedUICommand NewTabCommand = new RoutedUICommand( );
		public static readonly RoutedUICommand CloseTabCommand = new RoutedUICommand( );


		public MainWindow( )
		{
			InitializeComponent( );

			var MIN_INTERVAL = TimeSpan.FromSeconds( 5 );
			var interval = Properties.Settings.Default.AutoSaveInterval;
			if( interval < MIN_INTERVAL ) interval = MIN_INTERVAL;

			AutoSaveLoop = new ResumableLoop( AutoSaveThreadProc, (int)interval.TotalMilliseconds );
		}


		private void Window_Initialized( object sender, EventArgs e )
		{

		}


		private void Window_SourceInitialized( object sender, EventArgs e )
		{
			TryRestoreWindowPlacement( );
			RestoreMaximisedState( );
		}


		private void Window_Loaded( object sender, RoutedEventArgs e )
		{
			if( IsFullyLoaded ) return;

			List<TabData> all_tab_data = TryLoadAllTabData( );

			if( all_tab_data == null || !all_tab_data.Any( ) )
			{
				CreateTab( null );
			}
			else
			{
				TabItem first_tab = null;

				foreach( var tab_data in all_tab_data )
				{
					var tab = CreateTab( tab_data );

					if( first_tab == null ) first_tab = tab;
				}

				if( first_tab != null ) tabControlMain.SelectedItem = first_tab;
			}

			TrySwitchToSingleMode( );

			IsFullyLoaded = true;
		}


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		private void Window_Closing( object sender, System.ComponentModel.CancelEventArgs e )
		{
			AutoSaveLoop.SendStop( );

			try
			{
				SaveAllTabData( );
			}
			catch( Exception exc )
			{
				if( Debugger.IsAttached ) Debugger.Break( );
				else Debug.Fail( exc.Message, exc.ToString( ) );

				// ignore
			}

			SaveWindowPlacement( );
		}


		void UCMain_Changed( object sender, EventArgs e )
		{
			if( !IsFullyLoaded ) return;

			AutoSaveLoop.SendRestart( );
		}


		private void UCMain_NewTabClicked( object sender, EventArgs e )
		{
			NewTab( );
		}


		private void NewTabCommand_CanExecute( object sender, CanExecuteRoutedEventArgs e )
		{
			e.CanExecute = true;
		}


		private void NewTabCommand_Execute( object sender, ExecutedRoutedEventArgs e )
		{
			NewTab( );
		}


		private void CloseTabCommand_CanExecute( object sender, CanExecuteRoutedEventArgs e )
		{
			e.CanExecute = tabControlMain.IsVisible && ( tabControlMain.SelectedItem as TabItem )?.Content is UCMain;
		}


		private void CloseTabCommand_Execute( object sender, ExecutedRoutedEventArgs e )
		{
			TabItem tab_item = ( e.Parameter as TabItem ) ?? ( tabControlMain.IsVisible ? tabControlMain.SelectedItem as TabItem : null );

			if( tab_item != null && tab_item.Content is UCMain )
			{
				CloseTab( tab_item );
			}
			else
			{
				SystemSounds.Beep.Play( );
			}
		}



		// --------------------



		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		static List<TabData> TryLoadAllTabData( )
		{
			try
			{
				var all_tab_data = new List<TabData>( );

				string json = Properties.Settings.Default.SavedTabData;

				using( var ms = new MemoryStream( Encoding.UTF8.GetBytes( json ) ) )
				{
					var ser = new DataContractJsonSerializer( all_tab_data.GetType( ) );

					all_tab_data = ser.ReadObject( ms ) as List<TabData>;

					return all_tab_data;
				}
			}
			catch
			{
				// ignore

				return null;
			}
		}

		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void SaveAllTabData( )
		{
			var all_tab_data = new List<TabData>( );

			if( tabControlMain.IsVisible )
			{
				foreach( var tab_item in tabControlMain.Items.OfType<TabItem>( ) )
				{
					switch( tab_item.Content )
					{
					case UCMain uc_main:
					{
						var tab_data = new TabData( );

						tab_data.Name = tab_item.Header as string; //
						uc_main.ExportTabData( tab_data );

						all_tab_data.Add( tab_data );
					}
					break;
					}
				}
			}
			else
			{
				var uc_main = GetSingleModeControl( );
				if( uc_main != null )
				{
					var tab_data = new TabData( );

					uc_main.ExportTabData( tab_data );

					all_tab_data.Add( tab_data );
				}
			}

			string json;

			{
				using( var ms = new MemoryStream( ) )
				{
					using( var json_writer =
							JsonReaderWriterFactory.CreateJsonWriter( ms, Encoding.UTF8,
								ownsStream: false, indent: true, "  " ) )
					{
						var ser = new DataContractJsonSerializer( all_tab_data.GetType( ) );
						ser.WriteObject( json_writer, all_tab_data );
					}

					ms.Position = 0;

					using( var sr = new StreamReader( ms, Encoding.UTF8 ) )
					{
						json = sr.ReadToEnd( );
					}
				}
			}

			Properties.Settings.Default.SavedTabData = Environment.NewLine + json + Environment.NewLine;

			try
			{
				Properties.Settings.Default.Save( );
			}
			catch( Exception exc )
			{
				// e.g.: the file is read-only
				_ = exc;
				if( Debugger.IsAttached ) Debugger.Break( );

				// ignore
			}
		}


		TabItem CreateTab( TabData tabData )
		{
			int max =
				tabControlMain.Items
					.OfType<TabItem>( )
					.Where( i => i != tabNew && i.Header is string )
					.Select( i =>
					{
						var m = Regex.Match( (string)i.Header, @"^Tab\s*(\d+)$" );
						if( m.Success )
						{
							return int.Parse( m.Groups[1].Value, CultureInfo.InvariantCulture );
						}
						else
						{
							return 0;
						}
					} )
					.Concat( new[] { 0 } )
					.Max( );


			var newTabItem = new TabItem( );
			//newTabItem.Header = string.IsNullOrWhiteSpace( tab_data?.Name ) ? $"Tab {max + 1}" : tab_data.Name;
			newTabItem.Header = $"Tab {max + 1}";
			newTabItem.HeaderTemplate = (DataTemplate)tabControlMain.Resources["TabTemplate"];

			var uc_main = new UCMain
			{
				Width = double.NaN,
				Height = double.NaN
			};

			newTabItem.Content = uc_main;

			tabControlMain.Items.Insert( tabControlMain.Items.IndexOf( tabNew ), newTabItem );

			if( tabData != null ) uc_main.ApplyTabData( tabData );

			uc_main.Changed += UCMain_Changed;

			tabControlMain.SelectedItem = newTabItem; //?

			return newTabItem;
		}


		UCMain GetSingleModeControl( )
		{
			Debug.Assert( gridMain.Children.OfType<UCMain>( ).Count( ) <= 1 );

			return gridMain.Children.OfType<UCMain>( ).FirstOrDefault( );
		}


		void TrySwitchToSingleMode( )
		{
			var main_tabs = tabControlMain.Items.OfType<TabItem>( ).Where( t => t.Content is UCMain );
			if( main_tabs.Count( ) == 1 )
			{
				var tab_item = main_tabs.First( );
				var uc_main = (UCMain)tab_item.Content;
				tab_item.Content = null;
				//tabControlMain.Items.Remove( tab_item ); -- should be kept
				tabControlMain.Visibility = Visibility.Collapsed;
				gridMain.Children.Add( uc_main );

				uc_main.ShowNewTabButton( true );
				uc_main.NewTabClicked -= UCMain_NewTabClicked;
				uc_main.NewTabClicked += UCMain_NewTabClicked;
			}
		}


		void NewTab( )
		{
			var uc_main = GetSingleModeControl( );

			if( uc_main != null )
			{
				gridMain.Children.Remove( uc_main );
				var first_tab = (TabItem)tabControlMain.Items[0];
				first_tab.Content = uc_main;
				tabControlMain.Visibility = Visibility.Visible;
				uc_main.ShowNewTabButton( false );
			}

			CreateTab( null );
		}


		void CloseTab( TabItem tabItem )
		{
			var index = tabControlMain.Items.IndexOf( tabItem );

			tabControlMain.SelectedItem = tabItem;

			var r = MessageBox.Show( this, "Remove this tab?", "WARNING",
				MessageBoxButton.OKCancel, MessageBoxImage.Exclamation,
				MessageBoxResult.OK, MessageBoxOptions.None );

			if( r != MessageBoxResult.OK ) return;

			tabControlMain.Items.Remove( tabItem );

			if( tabControlMain.Items[index] == tabNew ) --index;

			if( index < 0 )
			{
				CreateTab( null );
				index = 0;
			}

			tabControlMain.SelectedIndex = index;

			// rename tabs
			var main_tabs = tabControlMain.Items.OfType<TabItem>( ).Where( t => t.Content is UCMain );
			int i = 0;
			foreach( var tab in main_tabs )
			{
				var name = "Tab " + ( ++i );
				if( !name.Equals( tab.Header ) ) tab.Header = name;
			}

			TrySwitchToSingleMode( );
		}


		void AutoSaveThreadProc( ICancellable cnc )
		{
			Dispatcher.InvokeAsync( SaveAllTabData, DispatcherPriority.ApplicationIdle );
		}


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void SaveWindowPlacement( )
		{
			try
			{
				Properties.Settings.Default.IsMaximised = WindowState == WindowState.Maximized;
				if( Properties.Settings.Default.IsMaximised )
				{
					Properties.Settings.Default.RestoreBounds = RestoreBounds;
				}
				else
				{
					Properties.Settings.Default.RestoreBounds = new Rect( Left, Top, ActualWidth, ActualHeight );
				}

				Properties.Settings.Default.Save( );
			}
			catch( Exception exc )
			{
				if( Debugger.IsAttached ) Debugger.Break( );
				else Debug.Fail( exc.Message, exc.ToString( ) );

				// ignore
			}
		}




		[StructLayout( LayoutKind.Sequential )]
		struct POINT
		{
			public Int32 X;
			public Int32 Y;
		}


		[DllImport( "user32", SetLastError = true )]
		static extern IntPtr MonitorFromPoint( POINT pt, Int32 dwFlags );

		const Int32 MONITOR_DEFAULTTONULL = 0;


		static bool IsVisibleOnAnyMonitor( Point px )
		{
			POINT p = new POINT { X = (int)px.X, Y = (int)px.Y };

			return MonitorFromPoint( p, MONITOR_DEFAULTTONULL ) != IntPtr.Zero;
		}


		Point ToPixels( Point p )
		{
			Matrix transform = PresentationSource.FromVisual( this ).CompositionTarget.TransformFromDevice;

			Point r = transform.Transform( p );

			return r;
		}


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void TryRestoreWindowPlacement( )
		{
			try
			{
				var r = Properties.Settings.Default.RestoreBounds;
				if( !r.IsEmpty && r.Width > 0 && r.Height > 0 )
				{
					// check if the window is in working area
					// TODO: check if it works with different DPIs

					Point p1, p2;
					p1 = r.TopLeft;
					p1.Offset( 10, 10 );
					p2 = r.TopRight;
					p2.Offset( -10, 10 );

					if( IsVisibleOnAnyMonitor( ToPixels( p1 ) ) || IsVisibleOnAnyMonitor( ToPixels( p2 ) ) )
					{
						Left = r.Left;
						Top = r.Top;
						Width = Math.Max( 50, r.Width );
						Height = Math.Max( 40, r.Height );
					}
				}
				// Note. To work on secondary monitor, the 'Maximised' state is restored in 'Window_SourceInitialized'.
			}
			catch( Exception exc )
			{
				_ = exc;
				if( Debugger.IsAttached ) Debugger.Break( );

				// ignore
			}
		}


		void RestoreMaximisedState( )
		{
			// restore the Maximised state; this works for secondary monitors as well;
			// to avoid undesirable effects, call it from 'SourceInitialised'
			if( Properties.Settings.Default.IsMaximised )
			{
				WindowState = WindowState.Maximized;
			}
		}

		#region IDisposable Support

		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose( bool disposing )
		{
			if( !disposedValue )
			{
				if( disposing )
				{
					// TODO: dispose managed state (managed objects).

					using( AutoSaveLoop ) { }
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~MainWindow()
		// {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose( )
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose( true );
			// TODO: uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}

		#endregion

	}
}
