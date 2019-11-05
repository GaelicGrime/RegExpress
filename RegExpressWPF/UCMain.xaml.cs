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
		readonly ResumableLoop FindMatchesLoop;
		readonly ResumableLoop UpdateWhitespaceWarningLoop;
		readonly ResumableLoop ShowTextInfoLoop;

		readonly Regex RegexHasWhitespace = new Regex( "\t|([ ](\r|\n|$))|((\r|\n)$)", RegexOptions.Compiled | RegexOptions.ExplicitCapture );

		bool IsFullyLoaded = false;
		bool IsInChange = false;
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

			FindMatchesLoop = new ResumableLoop( FindMatchesThreadProc, 333, 555 );
			UpdateWhitespaceWarningLoop = new ResumableLoop( UpdateWhitespaceWarningThreadProc, 444, 777 );
			ShowTextInfoLoop = new ResumableLoop( ShowTextInfoThreadProc, 333, 555 );
		}


		public void ApplyTabData( TabData tabData )
		{
			Debug.Assert( !IsInChange );

			if( !IsFullyLoaded || !IsVisible )
			{
				InitialTabData = tabData;
			}
			else
			{
				InitialTabData = null;

				StopAll( );
				LoadTabData( tabData );
				RestartAll( );
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
				tabData.ShowSucceededGroupsOnly = InitialTabData.ShowSucceededGroupsOnly;
				tabData.ShowCaptures = InitialTabData.ShowCaptures;
				tabData.ShowWhiteSpaces = InitialTabData.ShowWhiteSpaces;
				tabData.Eol = InitialTabData.Eol;
			}
			else
			{
				tabData.Pattern = ucPattern.GetSimpleTextData( "\n" ).Text;
				tabData.Text = ucText.GetSimpleTextData( "\n" ).Text;
				tabData.RegexOptions = GetRegexOptions( );
				tabData.ShowFirstMatchOnly = cbShowFirstOnly.IsChecked == true;
				tabData.ShowSucceededGroupsOnly = cbShowSucceededGroupsOnly.IsChecked == true;
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

			ucPattern.SetFocus( );

			IsFullyLoaded = true;

			Debug.Assert( !IsInChange );

			if( IsVisible )
			{
				if( InitialTabData != null )
				{
					var tab_data = InitialTabData;
					InitialTabData = null;

					StopAll( );
					LoadTabData( tab_data );
					RestartAll( );
				}
				else
				{
					ucPattern.SetRegexOptions( GetRegexOptions( ), GetEolOption( ) );
				}
			}
		}


		private void UserControl_IsVisibleChanged( object sender, DependencyPropertyChangedEventArgs e )
		{
			Debug.Assert( !IsInChange );

			if( true.Equals( e.NewValue ) && IsFullyLoaded )
			{
				if( InitialTabData != null )
				{
					StopAll( );

					var tab_data = InitialTabData;
					InitialTabData = null;

					LoadTabData( tab_data );

					RestartAll( );
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
			if( IsInChange ) return;

			FindMatchesLoop.SendRestart( );
			UpdateWhitespaceWarningLoop.SendRestart( );

			Changed?.Invoke( this, null );
		}


		private void UcText_TextChanged( object sender, EventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( IsInChange ) return;

			FindMatchesLoop.SendRestart( );
			ShowTextInfoLoop.SendRestart( );
			UpdateWhitespaceWarningLoop.SendRestart( );

			Changed?.Invoke( this, null );
		}


		private void UcText_SelectionChanged( object sender, EventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( IsInChange ) return;

			ShowTextInfoLoop.SendRestart( );
		}


		private void ucText_GotKeyboardFocus( object sender, KeyboardFocusChangedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( IsInChange ) return;

			if( !ucTextHadFocus )
			{
				ucTextHadFocus = true;

				ShowTextInfoLoop.SendRestart( );
			}
		}


		private void UcText_LostFocus( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( IsInChange ) return;

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
			if( IsInChange ) return;

			var segments = ucMatches.GetUnderlinedSegments( );

			ucText.SetExternalUnderlining( segments, setSelection: Properties.Settings.Default.MoveCaretToUnderlinedText );
		}


		private void UcMatches_LostFocus( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( IsInChange ) return;

			ucText.SetExternalUnderlining( Enumerable.Empty<Segment>( ).ToList( ), setSelection: false );
		}


		private void CbOption_CheckedChanged( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( IsInChange ) return;

			UpdateRegexOptionsControls( );

			ucPattern.SetRegexOptions( GetRegexOptions( ), GetEolOption( ) );
			FindMatchesLoop.SendRestart( );

			Changed?.Invoke( this, null );
		}


		private void CbShowWhitespaces_CheckedChanged( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( IsInChange ) return;

			ucPattern.ShowWhiteSpaces( cbShowWhitespaces.IsChecked == true );
			ucText.ShowWhiteSpaces( cbShowWhitespaces.IsChecked == true );

			UpdateWhitespaceWarningLoop.SendRestart( );

			Changed?.Invoke( this, null );
		}


		private void LnkShowAll_Click( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( IsInChange ) return;

			cbShowFirstOnly.IsChecked = false;
		}


		private void LnkShowFirst_Click( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( IsInChange ) return;

			cbShowFirstOnly.IsChecked = true;
		}


		private void CbxEol_SelectionChanged( object sender, SelectionChangedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( IsInChange ) return;

			ucPattern.SetRegexOptions( GetRegexOptions( ), GetEolOption( ) );
			FindMatchesLoop.SendRestart( );
			ShowTextInfoLoop.SendRestart( );

			Changed?.Invoke( this, null );
		}


		// --------------------


		private void LoadTabData( TabData tabData )
		{
			Debug.Assert( !IsInChange );
			IsInChange = true;

			try
			{
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
				cbShowSucceededGroupsOnly.IsChecked = tabData.ShowSucceededGroupsOnly;
				cbShowCaptures.IsChecked = tabData.ShowCaptures;
				cbShowWhitespaces.IsChecked = tabData.ShowWhiteSpaces;

				foreach( var item in cbxEol.Items.Cast<ComboBoxItem>( ) )
				{
					item.IsSelected = (string)item.Tag == tabData.Eol;
				}
				if( cbxEol.SelectedItem == null ) ( (ComboBoxItem)cbxEol.Items[0] ).IsSelected = true;

				ucPattern.ShowWhiteSpaces( tabData.ShowWhiteSpaces );
				ucText.ShowWhiteSpaces( tabData.ShowWhiteSpaces );

				ucPattern.SetFocus( );

				ucPattern.SetRegexOptions( tabData.RegexOptions, tabData.Eol );

				ucPattern.SetText( tabData.Pattern );
				ucText.SetText( tabData.Text );
			}
			finally
			{
				IsInChange = false;
			}
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


		private void RestartAll( )
		{
			FindMatchesLoop.SendRestart( );
			ShowTextInfoLoop.SendRestart( );
			UpdateWhitespaceWarningLoop.SendRestart( );
		}


		private void StopAll( )
		{
			FindMatchesLoop.SendStop( );
			UpdateWhitespaceWarningLoop.SendStop( );
			ShowTextInfoLoop.SendStop( );
		}


		void FindMatchesThreadProc( ICancellable cnc )
		{
			string eol = null;
			string pattern = null;
			string text = null;
			bool first_only = false;
			RegexOptions options = RegexOptions.None;

			UITaskHelper.Invoke( this,
				( ) =>
				{
					eol = GetEolOption( );
					pattern = ucPattern.GetSimpleTextData( eol ).Text;
					if( cnc.IsCancelRequested ) return;
					text = ucText.GetSimpleTextData( eol ).Text;
					if( cnc.IsCancelRequested ) return;
					first_only = cbShowFirstOnly.IsChecked == true;
					options = GetRegexOptions( excludeIncompatibility: false );
				} );

			if( cnc.IsCancelRequested ) return;

			if( string.IsNullOrEmpty( pattern ) )
			{
				UITaskHelper.BeginInvoke( this,
					( ) =>
					{
						ucText.SetMatches( Enumerable.Empty<Match>( ).ToList( ), cbShowCaptures.IsChecked == true, GetEolOption( ) );
						ucMatches.ShowNoPattern( );
						lblMatches.Text = "Matches";
						pnlShowAll.Visibility = Visibility.Collapsed;
						pnlShowFirst.Visibility = Visibility.Collapsed;
					} );
			}
			else
			{
				Regex re = null;
				bool pattern_is_good = false;

				try
				{
					re = new Regex( pattern, options );
					pattern_is_good = true;
				}
				catch( Exception exc )
				{
					UITaskHelper.BeginInvoke( this, CancellationToken.None,
						( ) =>
						{
							ucText.SetMatches( Enumerable.Empty<Match>( ).ToList( ), cbShowCaptures.IsChecked == true, GetEolOption( ) );
							ucMatches.ShowError( exc );
							lblMatches.Text = "Error";
							pnlShowAll.Visibility = Visibility.Collapsed;
							pnlShowFirst.Visibility = Visibility.Collapsed;
						} );

					Debug.Assert( !pattern_is_good );
				}

				if( pattern_is_good )
				{
					MatchCollection matches0 = re.Matches( text ); // TODO: make it cancellable, or use timeout

					if( cnc.IsCancelRequested ) return;

					var matches_to_show = first_only ? matches0.Cast<Match>( ).Take( 1 ).ToList( ) : matches0.Cast<Match>( ).ToList( );

					if( cnc.IsCancelRequested ) return;

					UITaskHelper.BeginInvoke( this,
									( ) =>
									{
										ucText.SetMatches( matches_to_show, cbShowCaptures.IsChecked == true, GetEolOption( ) );
										ucMatches.SetMatches( text, matches_to_show, first_only, cbShowSucceededGroupsOnly.IsChecked == true, cbShowCaptures.IsChecked == true );

										lblMatches.Text = matches0.Count == 0 ? "Matches" : matches0.Count == 1 ? "1 match" : $"{matches0.Count:#,##0} matches";
										pnlShowAll.Visibility = first_only && matches0.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
										pnlShowFirst.Visibility = !first_only && matches0.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
									} );
				}
			}
		}


		void ShowTextInfoThreadProc( ICancellable cnc )
		{
			UITaskHelper.BeginInvoke( this,
				( ) =>
				{
					var td = ucText.GetTextData( GetEolOption( ) );

					if( cnc.IsCancelRequested ) return;

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
				} );
		}


		void UpdateWhitespaceWarningThreadProc( ICancellable cnc )
		{
			Visibility visibility = Visibility.Hidden;
			string eol = null;

			UITaskHelper.Invoke( this,
				( ) =>
				{
					if( !cbShowWhitespaces.IsChecked == true )
					{
						eol = GetEolOption( );
						var td = ucPattern.GetTextData( eol );

						if( cnc.IsCancelRequested ) return;

						if( RegexHasWhitespace.IsMatch( td.Text ) )
						{
							visibility = Visibility.Visible;
						}
					}
				} );

			if( cnc.IsCancelRequested ) return;

			UITaskHelper.Invoke( this,
				( ) =>
				{
					if( visibility == Visibility.Hidden && !cbShowWhitespaces.IsChecked == true )
					{
						var td = ucText.GetTextData( eol );

						if( cnc.IsCancelRequested ) return;

						if( RegexHasWhitespace.IsMatch( td.Text ) )
						{
							visibility = Visibility.Visible;
						}
					}

					lblWhitespaceWarning.Visibility = visibility;
				} );
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

					using( FindMatchesLoop ) { }
					using( UpdateWhitespaceWarningLoop ) { }
					using( ShowTextInfoLoop ) { }
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
