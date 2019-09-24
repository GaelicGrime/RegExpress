using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using RegExpressWPF.Code;


namespace RegExpressWPF
{
    /// <summary>
    /// Interaction logic for UCMain.xaml
    /// </summary>
    public partial class UCMain : UserControl
    {
        readonly TaskHelper FindMatchesTask = new TaskHelper( );
        bool IsFullyLoaded = false;
        TabData InitialTabData = null;

        class RegexOptionInfo
        {
            internal RegexOptions Option;
            internal string Note;

            public RegexOptionInfo( RegexOptions option, string note = null )
            {
                Option = option;
                Note = note;
            }
        }

        static readonly RegexOptionInfo[] SupportedRegexOptions = new[]
        {
            //new RegexOptionInfo(RegexOptions.Compiled),
            new RegexOptionInfo(RegexOptions.CultureInvariant),
            new RegexOptionInfo(RegexOptions.ECMAScript),
            new RegexOptionInfo(RegexOptions.ExplicitCapture),
            new RegexOptionInfo(RegexOptions.IgnoreCase),
            new RegexOptionInfo(RegexOptions.IgnorePatternWhitespace),
            new RegexOptionInfo(RegexOptions.Multiline, "('^', '$' at '\\n' too)"),
            new RegexOptionInfo(RegexOptions.RightToLeft),
            new RegexOptionInfo(RegexOptions.Singleline, "('.' matches '\\n' too)"),
        };


        public EventHandler Changed;
        public EventHandler NewTabClicked;


        public UCMain( )
        {
            InitializeComponent( );

            btnNewTab.Visibility = Visibility.Collapsed;
            lblTextLength.Visibility = Visibility.Collapsed;
            pnlShowAll.Visibility = Visibility.Collapsed;
            pnlShowFirst.Visibility = Visibility.Collapsed;

            cbShowWhitespaces.IsChecked = true;
        }


        public void ApplyTabData( TabData tabData )
        {
            if( !IsFullyLoaded )
            {
                InitialTabData = tabData;
            }
            else
            {
                LoadTabData( tabData );
            }
        }


        public void ExportTabData( TabData tabData )
        {
            tabData.Pattern = ucPattern.GetText( "\n" );
            tabData.Text = ucText.GetText( "\n" );
            tabData.RegexOptions = GetRegexOptions( );
            tabData.ShowFirstMatchOnly = cbShowFirstOnly.IsChecked == true;
            tabData.ShowCaptures = cbShowCaptures.IsChecked == true;
            tabData.Eol = GetEolOption( );
        }


        public void ShowNewTabButon( bool yes )
        {
            btnNewTab.Visibility = yes ? Visibility.Visible : Visibility.Collapsed;
        }


        private void UserControl_Loaded( object sender, RoutedEventArgs e )
        {
            if( IsFullyLoaded ) return;

            foreach( var o in SupportedRegexOptions )
            {
                var s = o.Option.ToString( );
                if( o.Note != null ) s += ' ' + o.Note;
                var cb = new CheckBox { Content = s, Tag = o.Option };

                cb.Checked += CbOption_CheckedChanged;
                cb.Unchecked += CbOption_CheckedChanged;

                pnlRegexOptions.Children.Add( cb );
            }

            if( InitialTabData != null )
            {
                LoadTabData( InitialTabData );
                InitialTabData = null;
            }

            ucPattern.SetRegexOptions( GetRegexOptions( ) );

            ucPattern.SetFocus( );

            IsFullyLoaded = true;

            RestartFindMatches( );
        }


        private void BtnNewTab_Click( object sender, EventArgs e )
        {
            NewTabClicked?.Invoke( this, null );
        }


        private void UcPattern_TextChanged( object sender, EventArgs e )
        {
            if( !IsFullyLoaded ) return;

            RestartFindMatches( );
            Changed?.Invoke( this, null );
        }


        private void UcText_TextChanged( object sender, EventArgs e )
        {
            if( !IsFullyLoaded ) return;

            RestartFindMatches( );
            Changed?.Invoke( this, null );
        }


        private void UcText_SelectionChanged( object sender, EventArgs e )
        {
            if( !IsFullyLoaded ) return;
        }


        private void UcText_LostFocus( object sender, RoutedEventArgs e )
        {
            if( !IsFullyLoaded ) return;

            ucMatches.SetUnderlining( null );
        }


        private void UcText_LocalUnderliningFinished( object sender, EventArgs e )
        {
            if( ucText.IsKeyboardFocusWithin )
            {
                var underlining_info = ucText.GetUnderliningInfo( );
                var segments = underlining_info.Select( c => new Segment( c.Index, c.Length ) ).ToList( );
                ucMatches.SetUnderlining( segments );
            }
            else
            {
                ucMatches.SetUnderlining( null );
            }
        }


        private void UcMatches_SelectionChanged( object sender, EventArgs e )
        {
            if( !IsFullyLoaded ) return;

            var segments = ucMatches.GetUnderlinedSegments( );

            ucText.SetUnderlinedCaptures( segments );
        }


        private void UcMatches_LostFocus( object sender, RoutedEventArgs e )
        {
            if( !IsFullyLoaded ) return;

            ucText.SetUnderlinedCaptures( Enumerable.Empty<Segment>( ).ToList( ) );
        }


        private void CbOption_CheckedChanged( object sender, RoutedEventArgs e )
        {
            if( !IsFullyLoaded ) return;

            ucPattern.SetRegexOptions( GetRegexOptions( ) );
            RestartFindMatches( );
            Changed?.Invoke( this, null );
        }


        private void CbShowWhitespaces_CheckedChanged( object sender, RoutedEventArgs e )
        {
            if( !IsFullyLoaded ) return;

            Changed?.Invoke( this, null );
        }


        private void LnkShowAll_Click( object sender, RoutedEventArgs e )
        {
            if( !IsFullyLoaded ) return;

            cbShowFirstOnly.IsChecked = false;
        }


        private void LnkShowFirst_Click( object sender, RoutedEventArgs e )
        {
            if( !IsFullyLoaded ) return;

            cbShowFirstOnly.IsChecked = true;
        }


        private void CbxEol_SelectionChanged( object sender, SelectionChangedEventArgs e )
        {
            if( !IsFullyLoaded ) return;

            RestartFindMatches( );
            Changed?.Invoke( this, null );
        }


        // --------------------


        private void LoadTabData( TabData tabData )
        {
            ucPattern.Text = tabData.Pattern;
            ucText.Text = tabData.Text;

            foreach( var cb in pnlRegexOptions.Children.OfType<CheckBox>( ) )
            {
                var opt = cb.Tag as RegexOptions?;
                if( opt != null )
                {
                    cb.IsChecked = ( tabData.RegexOptions & opt.Value ) != 0;
                }
            }

            cbShowFirstOnly.IsChecked = tabData.ShowFirstMatchOnly;
            cbShowCaptures.IsChecked = tabData.ShowCaptures;
            cbShowWhitespaces.IsChecked = true; //

            foreach( var item in cbxEol.Items.Cast<ComboBoxItem>( ) )
            {
                item.IsSelected = (string)item.Tag == tabData.Eol;
            }
            if( cbxEol.SelectedItem == null ) ( (ComboBoxItem)cbxEol.Items[0] ).IsSelected = true;
        }


        RegexOptions GetRegexOptions( )
        {
            RegexOptions regex_options = RegexOptions.None;

            foreach( var cb in pnlRegexOptions.Children.OfType<CheckBox>( ) )
            {
                var opt = cb.Tag as RegexOptions?;
                if( opt != null )
                {
                    regex_options |= ( cb.IsChecked == true ? opt.Value : 0 );
                }
            }

            return regex_options;
        }


        string GetEolOption( )
        {
            var eol_item = (ComboBoxItem)cbxEol.SelectedItem;
            string eol = (string)eol_item.Tag;

            return eol;
        }


        void RestartFindMatches( )
        {
            FindMatchesTask.Stop( );

            string eol = GetEolOption( );

            string pattern = ucPattern.GetText( eol );
            string text = ucText.GetText( eol );
            bool find_all = cbShowFirstOnly.IsChecked != true;
            RegexOptions options = GetRegexOptions( );

            FindMatchesTask.Restart( ct => FindMatchesTaskProc( ct, pattern, text, find_all, options ) );

            lblTextLength.Text = $"({text.Length:#,##0} character{( text.Length == 1 ? "" : "s" )})";
            lblTextLength.Visibility = lblTextLength.Visibility == Visibility.Visible || text.Length != 0 ? Visibility.Visible : Visibility.Collapsed;
        }


        private void FindMatchesTaskProc( CancellationToken ct, string pattern, string text, bool findAll, RegexOptions options )
        {
            try
            {
                if( ct.WaitHandle.WaitOne( 333 ) ) return;
                ct.ThrowIfCancellationRequested( );

                if( string.IsNullOrEmpty( pattern ) )
                {
                    Dispatcher.BeginInvoke( new Action( ( ) =>
                    {
                        ucText.SetMatches( Enumerable.Empty<Match>( ).ToList( ).AsReadOnly( ), cbShowCaptures.IsChecked == true, GetEolOption( ) );
                        ucMatches.ShowNoPattern( );
                        lblMatches.Text = "Matches";
                        pnlShowAll.Visibility = Visibility.Collapsed;
                        pnlShowFirst.Visibility = Visibility.Collapsed;
                    } ) );

                    return;
                }

                var re = new Regex( pattern, options );

                MatchCollection matches0 = re.Matches( text ); // TODO: make it cancellable

                ct.ThrowIfCancellationRequested( );

                var matches_to_show = findAll ? matches0.Cast<Match>( ).ToList( ) : matches0.Cast<Match>( ).Take( 1 ).ToList( );

                Dispatcher.BeginInvoke( new Action( ( ) =>
                {
                    ucText.SetMatches( matches_to_show, cbShowCaptures.IsChecked == true, GetEolOption( ) );
                    ucMatches.SetMatches( text, matches_to_show, findAll, cbShowCaptures.IsChecked == true );

                    lblMatches.Text = matches0.Count == 0 ? "Matches" : matches0.Count == 1 ? "1 match" : $"{matches0.Count:#,##0} matches";
                    pnlShowAll.Visibility = !findAll && matches0.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
                    pnlShowFirst.Visibility = findAll && matches0.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
                } ) );
            }
            catch( OperationCanceledException ) // also 'TaskCanceledException'
            {
            }
            catch( Exception exc )
            {
                Dispatcher.BeginInvoke( new Action( ( ) =>
                {
                    ucText.SetMatches( Enumerable.Empty<Match>( ).ToList( ), cbShowCaptures.IsChecked == true, GetEolOption( ) );
                    ucMatches.ShowError( ct, exc );
                    lblMatches.Text = "Error";
                    pnlShowAll.Visibility = Visibility.Collapsed;
                    pnlShowFirst.Visibility = Visibility.Collapsed;
                } ) );
            }
        }
    }
}
