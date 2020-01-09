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
using RegexEngineInfrastructure;
using RegexEngineInfrastructure.Matches;
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

		// TODO: The active engine will be selectable.
		IRegexEngine CurrentRegexEngine = null;


		bool IsFullyLoaded = false;
		bool IsInChange = false;
		TabData InitialTabData = null;
		bool ucTextHadFocus = false;


		public event EventHandler Changed;
		public event EventHandler NewTabClicked;


		public UCMain( )
		{
			InitializeComponent( );

			CurrentRegexEngine = new DotNetRegexEngine.DotNetRegexEngine( ); // the default

			btnNewTab.Visibility = Visibility.Collapsed;
			lblTextInfo.Visibility = Visibility.Collapsed;
			pnlShowAll.Visibility = Visibility.Collapsed;
			pnlShowFirst.Visibility = Visibility.Collapsed;
			lblWarnings.Inlines.Remove( lblWhitespaceWarning1 );
			lblWarnings.Inlines.Remove( lblWhitespaceWarning2 );

			FindMatchesLoop = new ResumableLoop( FindMatchesThreadProc, 333, 555 );
			UpdateWhitespaceWarningLoop = new ResumableLoop( UpdateWhitespaceWarningThreadProc, 444, 777 );
			ShowTextInfoLoop = new ResumableLoop( ShowTextInfoThreadProc, 333, 555 );

			UpdateWhitespaceWarningLoop.Priority = ThreadPriority.Lowest;
			ShowTextInfoLoop.Priority = ThreadPriority.Lowest;
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
				tabData.RegexEngineId = InitialTabData.RegexEngineId;
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
				tabData.RegexEngineId = CurrentRegexEngine.Id;
				tabData.RegexOptions = GetRegexOptions( ).Select( o => o.AsText ).ToArray( );
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

			foreach( var o in CurrentRegexEngine.AllOptions )
			{
				var s = o.Text;
				if( !string.IsNullOrWhiteSpace( o.Note ) ) s += ' ' + o.Note;
				var cb = new CheckBox { Content = s, Tag = o };

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
					ucPattern.SetRegexOptions( CurrentRegexEngine, GetRegexOptions( ), GetEolOption( ) );
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

			ucPattern.SetRegexOptions( CurrentRegexEngine, GetRegexOptions( ), GetEolOption( ) );
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

			ucPattern.SetRegexOptions( CurrentRegexEngine, GetRegexOptions( ), GetEolOption( ) );
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
				// TODO: determine the engine

				var dotnet_engine = new DotNetRegexEngine.DotNetRegexEngine( );

				string[] options_as_text;

				switch( tabData.RegexOptions )
				{
				case int flags: // legacy format; replaced on save with 'string[]'
					options_as_text = dotnet_engine.ParseLegacyOptions( flags );
					break;
				case object[] arr:
					options_as_text = arr.OfType<string>( ).ToArray( );
					break;
				case null:
				default:
					options_as_text = new string[] { };
					break;
				}

				// TODO: implement multiple engines; use 'tabData.EngineId'
				CurrentRegexEngine = dotnet_engine;

				foreach( var cb in pnlRegexOptions.Children.OfType<CheckBox>( ) )
				{
					var opt = cb.Tag as IRegexOptionInfo;
					if( opt != null )
					{
						cb.IsChecked = options_as_text.Contains( opt.AsText );
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

				IRegexOptionInfo[] options = CurrentRegexEngine.AllOptions.Where( o => options_as_text.Contains( o.AsText ) ).ToArray( );

				ucPattern.SetRegexOptions( CurrentRegexEngine, options, tabData.Eol );

				ucPattern.SetText( tabData.Pattern );
				ucText.SetText( tabData.Text );
			}
			finally
			{
				IsInChange = false;
			}
		}


		IReadOnlyCollection<IRegexOptionInfo> GetRegexOptions( )
		{
			var regex_options = new List<IRegexOptionInfo>( );

			foreach( var cb in pnlRegexOptions.Children.OfType<CheckBox>( ).Where( x => x.IsChecked == true ) )
			{
				var opt = cb.Tag as IRegexOptionInfo;
				if( opt != null )
				{
					regex_options.Add( opt );
				}
			}

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


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void FindMatchesThreadProc( ICancellable cnc )
		{
			string eol = null;
			string pattern = null;
			string text = null;
			bool first_only = false;
			IReadOnlyCollection<IRegexOptionInfo> options = null;

			UITaskHelper.Invoke( this,
				( ) =>
				{
					eol = GetEolOption( );
					pattern = ucPattern.GetSimpleTextData( eol ).Text;
					if( cnc.IsCancellationRequested ) return;
					text = ucText.GetSimpleTextData( eol ).Text;
					if( cnc.IsCancellationRequested ) return;
					first_only = cbShowFirstOnly.IsChecked == true;
					options = GetRegexOptions( );
				} );

			if( cnc.IsCancellationRequested ) return;

			if( string.IsNullOrEmpty( pattern ) )
			{
				UITaskHelper.BeginInvoke( this,
					( ) =>
					{
						ucText.SetMatches( RegexMatches.Empty, cbShowCaptures.IsChecked == true, GetEolOption( ) );
						ucMatches.ShowNoPattern( );
						lblMatches.Text = "Matches";
						pnlShowAll.Visibility = Visibility.Collapsed;
						pnlShowFirst.Visibility = Visibility.Collapsed;
					} );
			}
			else
			{
				IParsedPattern parsed_pattern = null;
				bool pattern_is_good = false;

				try
				{
					parsed_pattern = CurrentRegexEngine.ParsePattern( pattern, options );
					pattern_is_good = true;
				}
				catch( Exception exc )
				{
					UITaskHelper.BeginInvoke( this, CancellationToken.None,
						( ) =>
						{
							ucText.SetMatches( RegexMatches.Empty, cbShowCaptures.IsChecked == true, GetEolOption( ) );
							ucMatches.ShowError( exc );
							lblMatches.Text = "Error";
							pnlShowAll.Visibility = Visibility.Collapsed;
							pnlShowFirst.Visibility = Visibility.Collapsed;
						} );

					Debug.Assert( !pattern_is_good );
				}

				if( pattern_is_good )
				{
					RegexMatches matches = parsed_pattern.Matches( text ); // TODO: make it cancellable, or use timeout



					{
						//........................
						try
						{
							var eng = new CppRegexEngine.CppRegexEngine( );
							//Thread.Sleep( 333 );
							var pp = eng.ParsePattern( pattern, Enumerable.Empty<IRegexOptionInfo>( ).ToList( ) );
							//Thread.Sleep( 333 );
							RegexMatches ms = pp.Matches( text );

							matches = ms;
						}
						catch
						{

						}
					}









					int count = matches.Count;

					if( cnc.IsCancellationRequested ) return;

					var matches_to_show = first_only ?
						new RegexMatches( Math.Min( 1, count ), matches.Matches.Take( 1 ) ) :
						matches;

					if( cnc.IsCancellationRequested ) return;

					UITaskHelper.BeginInvoke( this,
									( ) =>
									{
										ucText.SetMatches( matches_to_show, cbShowCaptures.IsChecked == true, GetEolOption( ) );
										ucMatches.SetMatches( text, matches_to_show, first_only, cbShowSucceededGroupsOnly.IsChecked == true, cbShowCaptures.IsChecked == true );

										lblMatches.Text = count == 0 ? "Matches" : count == 1 ? "1 match" : $"{count:#,##0} matches";
										pnlShowAll.Visibility = first_only && count > 1 ? Visibility.Visible : Visibility.Collapsed;
										pnlShowFirst.Visibility = !first_only && count > 1 ? Visibility.Visible : Visibility.Collapsed;
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

					if( cnc.IsCancellationRequested ) return;

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
			bool has_whitespaces = false;
			bool show_whitespaces_option = false;
			string eol = null;
			SimpleTextData td = null;

			UITaskHelper.Invoke( this,
				( ) =>
				{
					show_whitespaces_option = cbShowWhitespaces.IsChecked == true;
					eol = GetEolOption( );
					td = ucPattern.GetSimpleTextData( eol );

					if( cnc.IsCancellationRequested ) return;
				} );

			if( cnc.IsCancellationRequested ) return;

			has_whitespaces = RegexHasWhitespace.IsMatch( td.Text );

			if( !has_whitespaces )
			{
				UITaskHelper.Invoke( this,
					( ) =>
					{
						td = ucText.GetSimpleTextData( eol );
					} );

				has_whitespaces = RegexHasWhitespace.IsMatch( td.Text );
			}

			bool show1 = false;
			bool show2 = false;

			if( show_whitespaces_option )
			{
				if( has_whitespaces )
				{
					show2 = true;
				}
			}
			else
			{
				if( has_whitespaces )
				{
					show1 = true;
				}
			}

			UITaskHelper.Invoke( this,
				( ) =>
				{
					if( show1 && lblWhitespaceWarning1.Parent == null ) lblWarnings.Inlines.Add( lblWhitespaceWarning1 );
					if( !show1 && lblWhitespaceWarning1.Parent != null ) lblWarnings.Inlines.Remove( lblWhitespaceWarning1 );
					if( show2 && lblWhitespaceWarning2.Parent == null ) lblWarnings.Inlines.Add( lblWhitespaceWarning2 );
					if( !show2 && lblWhitespaceWarning2.Parent != null ) lblWarnings.Inlines.Remove( lblWhitespaceWarning2 );
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
