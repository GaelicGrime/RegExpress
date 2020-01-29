﻿using System;
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


namespace BoostRegexEngineNs
{
	/// <summary>
	/// Interaction logic for UCBoostRegexOptions.xaml
	/// </summary>
	public partial class UCBoostRegexOptions : UserControl
	{
		internal event EventHandler Changed;
		internal string[] CachedOptions; // (accessible from threads)


		bool IsFullyLoaded = false;
		int ChangeCounter = 0;


		public UCBoostRegexOptions( )
		{
			InitializeComponent( );

			// insert checkboxes

			{
				List<BoostRegexInterop.OptionInfo> compile_options = BoostRegexInterop.Matcher.GetCompileOptions( );

				foreach( var o in compile_options )
				{
					var tb = new TextBlock( );
					new Run( o.FlagName, tb.ContentEnd );
					new Run( " – " + o.Note, tb.ContentEnd );

					var cb = new CheckBox
					{
						Tag = o.FlagName,
						//Content = ( o.FlagName + " – " + o.Note ).Replace( "_", "__" )
						Content = tb
					};

					pnlCompileOptions.Children.Add( cb );
				}
			}

			{
				List<BoostRegexInterop.OptionInfo> match_options = BoostRegexInterop.Matcher.GetMatchOptions( );

				foreach( var o in match_options )
				{
					var cb = new CheckBox
					{
						Tag = o.FlagName,
						Content = ( o.FlagName + " – " + o.Note ).Replace( "_", "__" )
					};

					pnlMatchOptions.Children.Add( cb );
				}

			}
		}


		internal object ToSerialisableObject( )
		{
			return GetSelectedOptions( );
		}


		internal void FromSerializableObject( object obj )
		{
			string[] arr = obj as string[];

			if( arr == null )
			{
				if( obj is object[] ) arr = ( (object[])obj ).OfType<string>( ).ToArray( );
			}

			SetSelectedOptions( arr );
		}


		internal string[] GetSelectedOptions( )
		{
			return
				( new[] { ( (ComboBoxItem)cbxGrammar.SelectedItem )?.Tag.ToString( ) ?? "ECMAScript" } )
				.Concat(
				pnlCompileOptions.Children.OfType<CheckBox>( )
					.Where( cb => cb.IsChecked == true )
					.Select( cb => cb.Tag.ToString( ) )
				)
				.Concat(
				pnlMatchOptions.Children.OfType<CheckBox>( )
					.Where( cb => cb.IsChecked == true )
					.Select( cb => cb.Tag.ToString( ) )
				)
				.ToArray( );
		}


		internal void SetSelectedOptions( string[] options )
		{
			try
			{
				++ChangeCounter;

				options = options ?? new string[] { };

				var g = cbxGrammar.Items.Cast<ComboBoxItem>( ).FirstOrDefault( i => options.Contains( i.Tag.ToString( ) ) );
				if( g == null ) g = cbxGrammar.Items.Cast<ComboBoxItem>( ).FirstOrDefault( i => i.Tag.ToString( ) == "ECMAScript" );
				cbxGrammar.SelectedItem = g;

				foreach( var cb in pnlCompileOptions.Children.OfType<CheckBox>( ) )
				{
					cb.IsChecked = options.Contains( cb.Tag );
				}

				foreach( var cb in pnlMatchOptions.Children.OfType<CheckBox>( ) )
				{
					cb.IsChecked = options.Contains( cb.Tag );
				}
			}
			finally
			{
				--ChangeCounter;
			}
		}


		internal GrammarEnum GetGrammar( ) // (accessible from threads)
		{
			string grammar_s = Enum.GetNames( typeof( GrammarEnum ) ).FirstOrDefault( n => n != "None" && CachedOptions.Contains( n ) );
			if( grammar_s == null ) return GrammarEnum.None;

			return (GrammarEnum)Enum.Parse( typeof( GrammarEnum ), grammar_s );
		}


		internal bool GetModX( ) // (accessible from threads)
		{
			return CachedOptions.Contains( "mod_x" );
		}


		private void UserControl_Loaded( object sender, RoutedEventArgs e )
		{
			if( IsFullyLoaded ) return;

			CachedOptions = GetSelectedOptions( );

			IsFullyLoaded = true;
		}


		private void cbxGrammar_SelectionChanged( object sender, SelectionChangedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			CachedOptions = GetSelectedOptions( );

			Changed?.Invoke( null, null );
		}


		private void CheckBox_Changed( object sender, RoutedEventArgs e )
		{
			if( !IsFullyLoaded ) return;
			if( ChangeCounter != 0 ) return;

			CachedOptions = GetSelectedOptions( );

			Changed?.Invoke( null, null );
		}

	}
}
