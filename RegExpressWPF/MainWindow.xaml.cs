﻿using RegExpressWPF.Code;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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


namespace RegExpressWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly TaskHelper AutoSaveTask = new TaskHelper( );

        bool IsFullyLoaded = false;


        public static readonly RoutedUICommand NewTabCommand = new RoutedUICommand( );
        public static readonly RoutedUICommand CloseTabCommand = new RoutedUICommand( );


        public MainWindow( )
        {
            InitializeComponent( );
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
        }


        private void Window_Closing( object sender, System.ComponentModel.CancelEventArgs e )
        {
            AutoSaveTask.Stop( );

            try
            {
                SaveAllTabData( );
            }
            catch( Exception exc )
            {
                Debug.Fail( exc.Message, exc.ToString( ) );
            }
        }


        void UCMain_Changed( object sender, EventArgs e )
        {
            if( !IsFullyLoaded ) return;

            AutoSaveTask.Restart( AutoSaveTaskProc );
        }


        private void UCMain_NewTabClicked( object sender, EventArgs e )
        {
            NewTab( );
        }


        public void NewTabCommand_CanExecute( object sender, CanExecuteRoutedEventArgs e )
        {
            e.CanExecute = true;
        }


        public void NewTabCommand_Execute( object sender, ExecutedRoutedEventArgs e )
        {
            NewTab( );
        }


        public void CloseTabCommand_CanExecute( object sender, CanExecuteRoutedEventArgs e )
        {
            e.CanExecute = tabControlMain.IsVisible && ( tabControlMain.SelectedItem as TabItem )?.Content is UCMain;
        }


        public void CloseTabCommand_Execute( object sender, ExecutedRoutedEventArgs e )
        {
            TabItem tab_item = ( e.Parameter as TabItem ) ?? ( tabControlMain.IsVisible ? tabControlMain.SelectedItem as TabItem : null );

            if( tab_item != null && tab_item.Content is UCMain )
            {
                CloseTab( tab_item );
            }
            else
            {
                Console.Beep( );
            }
        }


        // --------------------


        List<TabData> TryLoadAllTabData( )
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
                return null;
            }
        }


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

            using( var ms = new MemoryStream( ) )
            {
                var ser = new DataContractJsonSerializer( all_tab_data.GetType( ) );
                ser.WriteObject( ms, all_tab_data );
                ms.Position = 0;

                using( var sr = new StreamReader( ms, Encoding.UTF8 ) )
                {
                    json = sr.ReadToEnd( );
                }
            }

            Properties.Settings.Default.SavedTabData = json;
            Properties.Settings.Default.Save( );
        }


        TabItem CreateTab( TabData tab_data )
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

            if( tab_data != null ) uc_main.ApplyTabData( tab_data );

            uc_main.Changed += UCMain_Changed;

            tabControlMain.SelectedItem = newTabItem; //...

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

                uc_main.ShowNewTabButon( true );
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
                uc_main.ShowNewTabButon( false );
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

            TrySwitchToSingleMode( );
        }


        void AutoSaveTaskProc( CancellationToken ct )
        {
            try
            {
                var MIN_INTERVAL = TimeSpan.FromSeconds( 1 );
                var interval = Properties.Settings.Default.AutoSaveInterval;
                if( interval < MIN_INTERVAL ) interval = MIN_INTERVAL;

                if( ct.WaitHandle.WaitOne( interval ) ) return;
                ct.ThrowIfCancellationRequested( );

                Dispatcher.InvokeAsync( new Action( ( ) =>
                {
                    SaveAllTabData( );
                } ),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                ct );
            }
            catch( OperationCanceledException ) // also 'TaskCanceledException'
            {
                // ignore
            }
            catch( Exception exc )
            {
                //throw;
                Debug.Fail( exc.Message, exc.ToString( ) );
            }
        }
    }
}
