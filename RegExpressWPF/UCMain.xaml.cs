using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
	public partial class UCMain : UserControl, IDisposable
	{
		readonly TaskHelper FindMatchesTask = new TaskHelper( );
		readonly TaskHelper UpdateWhitespaceWarningTask = new TaskHelper( );

		readonly Regex RegexHasWhitespace = new Regex( "\t|([ ](\r|\n|$))|(\r\n$)", RegexOptions.Compiled | RegexOptions.ExplicitCapture );

		bool IsFullyLoaded = false;
		TabData InitialTabData = null;
		bool ucTextHadFocus = false;


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


		public event EventHandler Changed;
		public event EventHandler NewTabClicked;


		public UCMain( )
		{
			InitializeComponent( );

			btnNewTab.Visibility = Visibility.Collapsed;
			lblTextInfo.Visibility = Visibility.Collapsed;
			pnlShowAll.Visibility = Visibility.Collapsed;
			pnlShowFirst.Visibility = Visibility.Collapsed;
			lblWhitespaceWarning.Visibility = Visibility.Hidden;
		}


		public void ApplyTabData( TabData tabData )
		{
			if( !IsFullyLoaded || !IsVisible )
			{
				InitialTabData = tabData;
			}
			else
			{
				InitialTabData = null;

				LoadTabData( tabData );
			}
		}


		public void ExportTabData( TabData tabData )
		{
			if( InitialTabData != null )
			{
				// did not have chance to finish initialisation 

				tabData.Pattern = InitialTabData.Pattern;
				tabData.Text = InitialTabData.Text;
				tabData.RegexOptions = InitialTabData.RegexOptions;
				tabData.ShowFirstMatchOnly = InitialTabData.ShowFirstMatchOnly;
				tabData.ShowFailedGroups = InitialTabData.ShowFailedGroups;
				tabData.ShowCaptures = InitialTabData.ShowCaptures;
				tabData.ShowWhiteSpaces = InitialTabData.ShowWhiteSpaces;
				tabData.Eol = InitialTabData.Eol;
			}
			else
			{
				tabData.Pattern = ucPattern.GetText( "\n" );
				tabData.Text = ucText.GetText( "\n" );
				tabData.RegexOptions = GetRegexOptions( );
				tabData.ShowFirstMatchOnly = cbShowFirstOnly.IsChecked == true;
				tabData.ShowFailedGroups = cbShowFailedGroups.IsChecked == true;
				tabData.ShowCaptures = cbShowCaptures.IsChecked == true;
				tabData.ShowWhiteSpaces = cbShowWhitespaces.IsChecked == true;
				tabData.Eol = GetEolOption( );
			}
		}


		public void ShowNewTabButton( bool yes )
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

			if( IsVisible )
			{
				if( InitialTabData != null )
				{
					var tab_data = InitialTabData;
					InitialTabData = null;

					LoadTabData( tab_data );
				}
			}

			ucPattern.SetRegexOptions( GetRegexOptions( ) );

			ucPattern.SetFocus( );

			IsFullyLoaded = true;

			RestartFindMatches( );
		}


		private void UserControl_IsVisibleChanged( object sender, DependencyPropertyChangedEventArgs e )
		{
			if( true.Equals( e.NewValue ) && IsFullyLoaded )
			{
				if( InitialTabData != null )
				{
					var tab_data = InitialTabData;
					InitialTabData = null;

					LoadTabData( tab_data );
				}
			}
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

			RestartUpdateWhitespaceWarning( );
		}


		private void UcText_TextChanged( object sender, EventArgs e )
		{
			if( !IsFullyLoaded ) return;

			RestartFindMatches( );
			Changed?.Invoke( this, null );

			RestartShowTextInfo( );
			RestartUpdateWhitespaceWarning( );
		}


		private void UcText_SelectionChanged( object sender, EventArgs e )
		{
			if( !IsFullyLoaded ) return;

			RestartShowTextInfo( );
		}


		private void ucText_GotKeyboardFocus( object sender, KeyboardFocusChangedEventArgs e )
		{
			if( !ucTextHadFocus )
			{
				ucTextHadFocus = true;

				RestartShowTextInfo( );
			}
		}


		private void UcText_LostFocus( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;

			ucMatches.SetExternalUnderlining( null, setSelection: false );
		}


		private void UcText_LocalUnderliningFinished( object sender, EventArgs e )
		{
			if( ucText.IsKeyboardFocusWithin )
			{
				var underlining_info = ucText.GetUnderliningInfo( );
				ucMatches.SetExternalUnderlining( underlining_info, setSelection: Properties.Settings.Default.MoveCaretToUnderlinedText );
			}
			else
			{
				ucMatches.SetExternalUnderlining( null, setSelection: false );
			}
		}


		private void UcMatches_SelectionChanged( object sender, EventArgs e )
		{
			if( !IsFullyLoaded ) return;

			var segments = ucMatches.GetUnderlinedSegments( );

			ucText.SetExternalUnderlining( segments, setSelection: Properties.Settings.Default.MoveCaretToUnderlinedText );
		}


		private void UcMatches_LostFocus( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;

			ucText.SetExternalUnderlining( Enumerable.Empty<Segment>( ).ToList( ), setSelection: false );
		}


		private void CbOption_CheckedChanged( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;

			UpdateRegexOptionsControls( );

			ucPattern.SetRegexOptions( GetRegexOptions( ) );
			RestartFindMatches( );
			Changed?.Invoke( this, null );
		}


		private void CbShowWhitespaces_CheckedChanged( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;

			ucPattern.ShowWhiteSpaces( cbShowWhitespaces.IsChecked == true );
			ucText.ShowWhiteSpaces( cbShowWhitespaces.IsChecked == true );

			Changed?.Invoke( this, null );

			RestartUpdateWhitespaceWarning( );
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

			RestartShowTextInfo( );
		}


		// --------------------


		private void LoadTabData( TabData tabData )
		{
			ucPattern.SetText( tabData.Pattern );
			ucText.SetText( tabData.Text );

			foreach( var cb in pnlRegexOptions.Children.OfType<CheckBox>( ) )
			{
				var opt = cb.Tag as RegexOptions?;
				if( opt != null )
				{
					cb.IsChecked = ( tabData.RegexOptions & opt.Value ) != 0;
				}
			}

			UpdateRegexOptionsControls( );

			cbShowFirstOnly.IsChecked = tabData.ShowFirstMatchOnly;
			cbShowFailedGroups.IsChecked = tabData.ShowFailedGroups;
			cbShowCaptures.IsChecked = tabData.ShowCaptures;
			cbShowWhitespaces.IsChecked = tabData.ShowWhiteSpaces;

			foreach( var item in cbxEol.Items.Cast<ComboBoxItem>( ) )
			{
				item.IsSelected = (string)item.Tag == tabData.Eol;
			}
			if( cbxEol.SelectedItem == null ) ( (ComboBoxItem)cbxEol.Items[0] ).IsSelected = true;

			ucPattern.ShowWhiteSpaces( tabData.ShowWhiteSpaces );
			ucText.ShowWhiteSpaces( tabData.ShowWhiteSpaces );

			RestartShowTextInfo( );
			RestartUpdateWhitespaceWarning( );
		}


		RegexOptions GetRegexOptions( bool excludeIncompatibility = false )
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

			Debug.Assert( !excludeIncompatibility );
#if false // Feature disabled, since does not look usefull.

            if( excludeIncompatibility )
            {
                if( regex_options.HasFlag( RegexOptions.ECMAScript ) )
                {
                    regex_options &= ~(
                        RegexOptions.ExplicitCapture |
                        RegexOptions.IgnorePatternWhitespace |
                        RegexOptions.RightToLeft
                        );
                }
            }
#endif

			return regex_options;
		}


		void UpdateRegexOptionsControls( )
		{
#if false // Feature disabled, since does not look usefull.

            RegexOptions regex_options = GetRegexOptions( );
            bool is_ecma = regex_options.HasFlag( RegexOptions.ECMAScript );
            RegexOptions ecma_incompatible =
                RegexOptions.ExplicitCapture |
                RegexOptions.IgnorePatternWhitespace |
                RegexOptions.RightToLeft;

            foreach( var cb in pnlRegexOptions.Children.OfType<CheckBox>( ) )
            {
                var opt = cb.Tag as RegexOptions?;
                if( opt != null )
                {
                    cb.IsEnabled = opt == RegexOptions.ECMAScript || !( is_ecma && ecma_incompatible.HasFlag( opt ) );
                }
            }
#endif
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
			RegexOptions options = GetRegexOptions( excludeIncompatibility: false );

			FindMatchesTask.Restart( ct => FindMatchesTaskProc( ct, pattern, text, find_all, options ) );
		}


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
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
						ucText.SetMatches( Enumerable.Empty<Match>( ).ToList( ), cbShowCaptures.IsChecked == true, GetEolOption( ) );
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
					ucMatches.SetMatches( text, matches_to_show, findAll, cbShowFailedGroups.IsChecked == true, cbShowCaptures.IsChecked == true );

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
					ucMatches.ShowError( exc );
					lblMatches.Text = "Error";
					pnlShowAll.Visibility = Visibility.Collapsed;
					pnlShowFirst.Visibility = Visibility.Collapsed;
				} ) );
			}
		}


		void RestartShowTextInfo( )
		{
			Dispatcher.BeginInvoke( new Action( ShowTextInfo ), DispatcherPriority.SystemIdle );
		}


		void ShowTextInfo( )
		{
			var td = ucText.GetTextData( GetEolOption( ) );

			lblTextInfo.Visibility = lblTextInfo.Visibility == Visibility.Visible || td.Text.Length != 0 ? Visibility.Visible : Visibility.Collapsed;
			if( lblTextInfo.Visibility == Visibility.Visible )
			{
				string s = $"({td.Text.Length:#,##0} character{( td.Text.Length == 1 ? "" : "s" )}";

				if( ucTextHadFocus )
				{
					s += $", Index: {td.SelectionStart:#,##0}";
				}

				s += ")";

				lblTextInfo.Text = s;
			}
		}


		void RestartUpdateWhitespaceWarning( )
		{
			UpdateWhitespaceWarningTask.Restart( UpdateWhitespaceWarning );
		}


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void UpdateWhitespaceWarning( CancellationToken ct )
		{
			try
			{
				if( ct.WaitHandle.WaitOne( 777 ) ) return;
				ct.ThrowIfCancellationRequested( );

				Dispatcher.BeginInvoke( new Action( ( ) =>
				 {
					 var visibility = Visibility.Hidden;

					 if( !cbShowWhitespaces.IsChecked == true )
					 {
						 var eol = GetEolOption( );
						 var td = ucPattern.GetTextData( eol );

						 if( RegexHasWhitespace.IsMatch( td.Text ) )
						 {
							 visibility = Visibility.Visible;
						 }
						 else
						 {
							 td = ucText.GetTextData( eol );

							 if( RegexHasWhitespace.IsMatch( td.Text ) )
							 {
								 visibility = Visibility.Visible;
							 }
						 }
					 }

					 lblWhitespaceWarning.Visibility = visibility;

				 } ),
				 DispatcherPriority.ApplicationIdle );
			}
			catch( OperationCanceledException ) // also 'TaskCanceledException'
			{
				// ignore
			}
			catch( Exception exc )
			{
				_ = exc;
				// TODO: report
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

					using( FindMatchesTask ) { }
					using( UpdateWhitespaceWarningTask ) { }
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~UCMain()
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
