using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
using RegExpressWPF.Adorners;
using RegExpressWPF.Code;


namespace RegExpressWPF
{
	/// <summary>
	/// Interaction logic for UCMatches.xaml
	/// </summary>
	public partial class UCMatches : UserControl, IDisposable
	{
		readonly UnderliningAdorner UnderliningAdorner;

		readonly TaskHelper ShowMatchesTask = new TaskHelper( );
		readonly TaskHelper UnderliningTask = new TaskHelper( );

		readonly ChangeEventHelper ChangeEventHelper;

		readonly StyleInfo[] HighlightStyleInfos;
		readonly StyleInfo[] HighlightLightStyleInfos;
		readonly StyleInfo MatchNormalStyleInfo;
		readonly StyleInfo MatchValueStyleInfo;
		readonly StyleInfo MatchValueSpecialStyleInfo;
		readonly StyleInfo LocationStyleInfo;
		readonly StyleInfo GroupNameStyleInfo;
		readonly StyleInfo GroupSiblingValueStyleInfo;
		readonly StyleInfo GroupValueStyleInfo;
		readonly StyleInfo GroupFailedStyleInfo;

		readonly LengthConverter LengthConverter = new LengthConverter( );

		bool AlreadyLoaded = false;

		IReadOnlyList<Match> LastMatches; // null if no data or processes unfinished
		bool LastShowFirstOnly;
		bool LastShowSucceededGroupsOnly;
		bool LastShowCaptures;

		abstract class Info
		{
			internal abstract MatchInfo GetMatchInfo( );
		}

		sealed class MatchInfo : Info
		{
			internal Segment MatchSegment;
			internal Span Span;
			internal Inline ValueInline;
			internal List<GroupInfo> GroupInfos = new List<GroupInfo>( );

			internal override MatchInfo GetMatchInfo( ) => this;
		}

		sealed class GroupInfo : Info
		{
			internal MatchInfo Parent;
			internal bool IsSuccess;
			internal Segment GroupSegment;
			internal Span Span;
			internal Inline ValueInline;
			internal List<CaptureInfo> CaptureInfos = new List<CaptureInfo>( );

			internal override MatchInfo GetMatchInfo( ) => Parent.GetMatchInfo( );
		}

		sealed class CaptureInfo : Info
		{
			internal GroupInfo Parent;
			internal Segment CaptureSegment;
			internal Span Span;
			internal Inline ValueInline;

			internal override MatchInfo GetMatchInfo( ) => Parent.GetMatchInfo( );
		}

		readonly List<MatchInfo> MatchInfos = new List<MatchInfo>( );


		public event EventHandler SelectionChanged;


		public UCMatches( )
		{
			InitializeComponent( );

			UnderliningAdorner = new UnderliningAdorner( rtbMatches );

			ChangeEventHelper = new ChangeEventHelper( rtbMatches );

			HighlightStyleInfos = new[]
			{
				new StyleInfo( "MatchHighlight_0" ),
				new StyleInfo( "MatchHighlight_1" ),
				new StyleInfo( "MatchHighlight_2" )
			};

			HighlightLightStyleInfos = new[]
			{
				new StyleInfo( "MatchHighlight_0_Light" ),
				new StyleInfo( "MatchHighlight_1_Light" ),
				new StyleInfo( "MatchHighlight_2_Light" )
			};

			MatchNormalStyleInfo = new StyleInfo( "MatchNormal" );
			MatchValueStyleInfo = new StyleInfo( "MatchValue" );
			MatchValueSpecialStyleInfo = new StyleInfo( "MatchValueSpecial" );
			LocationStyleInfo = new StyleInfo( "MatchLocation" );
			GroupNameStyleInfo = new StyleInfo( "MatchGroupName" );
			GroupSiblingValueStyleInfo = new StyleInfo( "MatchGroupSiblingValue" );
			GroupValueStyleInfo = new StyleInfo( "MatchGroupValue" );
			GroupFailedStyleInfo = new StyleInfo( "MatchGroupFailed" );

			pnlDebug.Visibility = Visibility.Collapsed;
			//UnderliningAdorner.IsDbgDisabled = true;
#if !DEBUG
			pnlDebug.Visibility = Visibility.Collapsed;
#endif
		}


		public void ShowError( Exception exc )
		{
			StopAll( );

			LastMatches = null;

			Dispatcher.BeginInvoke( new Action( ( ) =>
			{
				ShowOne( rtbError );
				runError.Text = exc.Message;
			} ) );
		}


		public void ShowNoPattern( )
		{
			StopAll( );

			LastMatches = null;

			Dispatcher.BeginInvoke( new Action( ( ) =>
			{
				ShowOne( rtbNoPattern );
			} ) );
		}


		public void SetMatches( string text, IReadOnlyList<Match> matches, bool showFirstOnly, bool showSucceededGroupsOnly, bool showCaptures )
		{
			if( matches == null ) throw new ArgumentNullException( nameof( matches ) );

			lock( this )
			{
				if( LastMatches != null )
				{
					var old_groups = LastMatches.SelectMany( m => m.Groups.Cast<Group>( ) ).Select( g => (g.Index, g.Length, g.Value, g.Name) );
					var new_groups = matches.SelectMany( m => m.Groups.Cast<Group>( ) ).Select( g => (g.Index, g.Length, g.Value, g.Name) );

					var old_captures = LastMatches.SelectMany( m => m.Groups.Cast<Group>( ) ).SelectMany( g => g.Captures.Cast<Capture>( ) ).Select( c => ( c.Value ) );
					var new_captures = matches.SelectMany( m => m.Groups.Cast<Group>( ) ).SelectMany( g => g.Captures.Cast<Capture>( ) ).Select( c => ( c.Value ) );

					if( showFirstOnly == LastShowFirstOnly &&
						showSucceededGroupsOnly == LastShowSucceededGroupsOnly &&
						showCaptures == LastShowCaptures &&
						new_groups.SequenceEqual( old_groups ) &&
						new_captures.SequenceEqual( old_captures ) )
					{
						LastMatches = matches;

						return;
					}
				}
			}

			ShowMatchesTask.Stop( );
			UnderliningTask.Stop( );
			UnderliningAdorner.SetRangesToUnderline( null );

			LastMatches = null;

			RestartShowMatches( text, matches, showFirstOnly, showSucceededGroupsOnly, showCaptures );
		}


		public void SetExternalUnderlining( IReadOnlyList<Segment> segments, bool setSelection )
		{
			if( segments == null ) segments = Enumerable.Empty<Segment>( ).ToList( );

			RestartExternalUnderline( segments, setSelection );
		}


		public IReadOnlyList<Segment> GetUnderlinedSegments( )
		{
			List<Segment> segments = new List<Segment>( );

			if( !rtbMatches.IsFocused ||
				LastMatches == null ||
				!LastMatches.Any( ) )
			{
				return segments;
			}

			TextSelection sel = rtbMatches.Selection;

			for( var parent = sel.Start.Parent; parent != null; )
			{
				object tag = null;

				switch( parent )
				{
				case FrameworkElement fe:
					tag = fe.Tag;
					parent = fe.Parent;
					break;
				case FrameworkContentElement fce:
					tag = fce.Tag;
					parent = fce.Parent;
					break;
				}

				switch( tag )
				{
				case MatchInfo mi:
					segments.Add( mi.MatchSegment );
					return segments;
				case GroupInfo gi:
					if( gi.IsSuccess ) segments.Add( gi.GroupSegment );
					return segments;
				case CaptureInfo ci:
					segments.Add( ci.CaptureSegment );
					return segments;
				}
			}

			return segments;
		}


		public void StopAll( )
		{
			ShowMatchesTask.Stop( );
			UnderliningTask.Stop( );
		}


		private void UserControl_Loaded( object sender, RoutedEventArgs e )
		{
			if( AlreadyLoaded ) return;

			rtbMatches.Document.MinPageWidth = (double)LengthConverter.ConvertFromString( "21cm" );

			var adorner_layer = AdornerLayer.GetAdornerLayer( rtbMatches );
			adorner_layer.Add( UnderliningAdorner );

			AlreadyLoaded = true;
		}


		private void rtbMatches_SelectionChanged( object sender, RoutedEventArgs e )
		{
			if( !IsLoaded ) return;
			if( ChangeEventHelper.IsInChange ) return;
			if( !rtbMatches.IsFocused ) return;

			RestartLocalUnderlining( true );
			SelectionChanged?.Invoke( this, null );

			ShowDebugInformation( ); // #if DEBUG
		}


		private void rtbMatches_GotFocus( object sender, RoutedEventArgs e )
		{
			RestartLocalUnderlining( true );
			SelectionChanged?.Invoke( this, null );

			if( Properties.Settings.Default.BringCaretIntoView )
			{
				var p = rtbMatches.CaretPosition.Parent as FrameworkContentElement;
				if( p != null )
				{
					p.BringIntoView( );
				}
			}
		}


		private void rtbMatches_LostFocus( object sender, RoutedEventArgs e )
		{
			RestartLocalUnderlining( false );
		}


		void RestartShowMatches( string text, IReadOnlyList<Match> matches, bool showFirstOnly, bool showSucceededGroupsOnly, bool showCaptures )
		{
			ShowMatchesTask.Stop( );
			UnderliningTask.Stop( );
			UnderliningAdorner.SetRangesToUnderline( null );

			MatchInfos.Clear( );

			ShowMatchesTask.Restart( ct => ShowMatchesTaskProc( ct, text, matches, showFirstOnly, showSucceededGroupsOnly, showCaptures ) );
		}


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void ShowMatchesTaskProc( CancellationToken ct, string text, IReadOnlyList<Match> matches, bool showFirstOnly, bool showSucceededGroupsOnly, bool showCaptures )
		{
			try
			{
				if( ct.WaitHandle.WaitOne( 333 ) ) return;
				ct.ThrowIfCancellationRequested( );

				if( matches.Count == 0 )
				{
					Dispatcher.BeginInvoke( new Action( ( ) =>
					{
						ShowOne( rtbNoMatches );
					} ) );

					lock( this )
					{
						LastMatches = matches;
						LastShowFirstOnly = showFirstOnly;
						LastShowSucceededGroupsOnly = showSucceededGroupsOnly;
						LastShowCaptures = showCaptures;
					}

					return;
				}

				ChangeEventHelper.Invoke( ct, ( ) =>
				{
					pbProgress.Maximum = matches.Count;
					pbProgress.Value = 0;

					if( secMatches.Blocks.Count > matches.Count )
					{
						// remove unneeded paragraphs
						var r = new TextRange( secMatches.Blocks.ElementAt( matches.Count ).ElementStart, secMatches.ContentEnd );
						r.Text = "";
					}

					ShowOne( rtbMatches );
				} );

				int show_pb_time = unchecked(Environment.TickCount + 333); // (ignore overflow)

				Paragraph previous_para = null;
				int match_index = -1;

				foreach( var match in matches )
				{
					Debug.Assert( match.Success );

					++match_index;

					ct.ThrowIfCancellationRequested( );

					var ordered_groups =
										match.Groups.Cast<Group>( )
											.Skip( 1 ) // skip match
											.Where( g => g.Success || !showSucceededGroupsOnly )
											//OrderBy( g => g.Success ? g.Index : match.Index )
											.ToList( );

					int min_index = ordered_groups.Select( g => g.Success ? g.Index : match.Index ).Concat( new[] { match.Index } ).Min( );
					if( showCaptures )
					{
						min_index = ordered_groups.SelectMany( g => g.Captures.Cast<Capture>( ) ).Select( c => c.Index ).Concat( new[] { min_index } ).Min( );
					}

					const int LEFT_WIDTH = 24;

					int left_width_for_match = LEFT_WIDTH + ( match.Index - min_index );

					Paragraph para = null;
					Run run = null;
					MatchInfo match_info = null;
					RunBuilder match_run_builder = new RunBuilder( MatchValueSpecialStyleInfo );

					var highlight_style = HighlightStyleInfos[match_index % HighlightStyleInfos.Length];
					var highlight_light_style = HighlightLightStyleInfos[match_index % HighlightStyleInfos.Length];

					// show match

					string match_name_text = showFirstOnly ? "Fɪʀꜱᴛ Mᴀᴛᴄʜ" : $"Mᴀᴛᴄʜ {match_index + 1}";

					ChangeEventHelper.Invoke( ct, ( ) =>
					{
						pbProgress.Value = match_index;
						if( Environment.TickCount >= show_pb_time ) pbProgress.Visibility = Visibility.Visible;

						var span = new Span( );

						para = new Paragraph( span );

						var start_run = new Run( match_name_text.PadRight( left_width_for_match, ' ' ), span.ContentEnd );
						start_run.Style( MatchNormalStyleInfo );

						Inline value_inline;

						if( match.Length == 0 )
						{
							value_inline = new Run( "(empty)", span.ContentEnd ); //
							value_inline.Style( MatchNormalStyleInfo, highlight_style, LocationStyleInfo );
						}
						else
						{
							value_inline = match_run_builder.Build( match.Value, span.ContentEnd );
							value_inline.Style( MatchValueStyleInfo, highlight_style );
						}

						run = new Run( $"\x200E  （{match.Index}, {match.Length}）", span.ContentEnd );
						run.Style( MatchNormalStyleInfo, LocationStyleInfo );

						_ = new LineBreak( span.ElementEnd ); // (after span)

						match_info = new MatchInfo
						{
							MatchSegment = new Segment( match.Index, match.Length ),
							Span = span,
							ValueInline = value_inline,
						};

						span.Tag = match_info;

						MatchInfos.Add( match_info );

						// captures for match
						//if( showCaptures) AppendCaptures( ct, para, LEFT_WIDTH, match, match );
					} );

					// show groups

					RunBuilder sibling_run_builder = new RunBuilder( null );

					foreach( var group in ordered_groups )
					{
						ct.ThrowIfCancellationRequested( );

						string group_name_text = $" • Gʀᴏᴜᴘ ‹{group.Name}›";
						int left_width_for_group = left_width_for_match - Math.Max( 0, match.Index - ( group.Success ? group.Index : match.Index ) );

						ChangeEventHelper.Invoke( ct, ( ) =>
						{
							var span = new Span( );

							var start_run = new Run( group_name_text.PadRight( left_width_for_group, ' ' ), span.ContentEnd );
							start_run.Style( GroupNameStyleInfo );

							// (NOTE. Overlaps are possible in this example: (?=(..))

							Inline value_inline;
							Inline inl;

							if( !group.Success )
							{
								value_inline = new Run( "(fail)", span.ContentEnd );
								value_inline.Style( GroupFailedStyleInfo );
							}
							else if( group.Length == 0 )
							{
								value_inline = new Run( "(empty)", span.ContentEnd );
								value_inline.Style( LocationStyleInfo );
							}
							else
							{
								string left = Utilities.SubstringFromTo( text, match.Index, group.Index );
								string middle = group.Value;
								string right = Utilities.SubstringFromTo( text, group.Index + group.Length, Math.Max( match.Index + match.Length, group.Index + group.Length ) );

								inl = sibling_run_builder.Build( left, span.ContentEnd );
								inl.Style( GroupSiblingValueStyleInfo );

								value_inline = match_run_builder.Build( middle, span.ContentEnd );
								value_inline.Style( GroupValueStyleInfo, highlight_light_style );

								inl = sibling_run_builder.Build( right, span.ContentEnd );
								inl.Style( GroupSiblingValueStyleInfo );
							}

							run = new Run( $"\x200E  （{group.Index}, {group.Length}）", span.ContentEnd );
							run.Style( MatchNormalStyleInfo, LocationStyleInfo );

							para.Inlines.Add( span );
							_ = new LineBreak( span.ElementEnd ); // (after span)

							var group_info = new GroupInfo
							{
								Parent = match_info,
								IsSuccess = group.Success,
								GroupSegment = new Segment( group.Index, group.Length ),
								Span = span,
								ValueInline = value_inline,
							};

							span.Tag = group_info;

							match_info.GroupInfos.Add( group_info );


							// captures for group
							if( showCaptures )
							{
								AppendCaptures( ct, group_info, para, left_width_for_match, text, match, group, highlight_light_style, match_run_builder, sibling_run_builder );
							}
						} );
					}

					ChangeEventHelper.Invoke( ct, ( ) =>
					{
						if( previous_para == null )
						{
							var first_block = secMatches.Blocks.FirstBlock;
							if( first_block == null )
							{
								secMatches.Blocks.Add( para );
							}
							else
							{
								secMatches.Blocks.InsertBefore( first_block, para );
								secMatches.Blocks.Remove( first_block );
							}
						}
						else
						{
							var next = previous_para.NextBlock;
							if( next != null ) secMatches.Blocks.Remove( next );
							secMatches.Blocks.InsertAfter( previous_para, para );
						}
					} );

					previous_para = para;
				}

				ct.ThrowIfCancellationRequested( );

				ChangeEventHelper.Invoke( ct, ( ) =>
				{
					pbProgress.Visibility = Visibility.Hidden;
				} );

				lock( this )
				{
					LastMatches = matches;
					LastShowFirstOnly = showFirstOnly;
					LastShowSucceededGroupsOnly = showSucceededGroupsOnly;
					LastShowCaptures = showCaptures;
				}
			}
			catch( OperationCanceledException exc ) // also 'TaskCanceledException'
			{
				Utilities.DbgSimpleLog( exc );

				// ignore
			}
			catch( Exception exc )
			{
				_ = exc;
				if( Debugger.IsAttached ) Debugger.Break( );
				throw;
			}
		}


		void AppendCaptures( CancellationToken ct, GroupInfo groupInfo, Paragraph para, int leftWidthForMatch,
			string text, Match match, Group group, StyleInfo highlightStyle,
			RunBuilder runBuilder, RunBuilder siblingRunBuilder )
		{
			int capture_index = -1;
			foreach( Capture capture in group.Captures )
			{
				if( ct.IsCancellationRequested ) break;

				++capture_index;

				var span = new Span( );

				string capture_name_text = $"  ◦ Cᴀᴘᴛᴜʀᴇ {capture_index}";
				int left_width_for_capture = leftWidthForMatch - Math.Max( 0, Math.Max( match.Index - group.Index, match.Index - capture.Index ) );

				var start_run = new Run( capture_name_text.PadRight( left_width_for_capture, ' ' ), span.ContentEnd );
				start_run.Style( GroupNameStyleInfo );

				Inline value_inline;
				Inline inline;

				if( capture.Length == 0 )
				{
					value_inline = new Run( "(empty)", span.ContentEnd );
					value_inline.Style( MatchNormalStyleInfo, LocationStyleInfo );
				}
				else
				{
					string left = Utilities.SubstringFromTo( text, Math.Min( match.Index, group.Index ), capture.Index );
					string middle = capture.Value;
					string right = Utilities.SubstringFromTo( text, capture.Index + capture.Length, Math.Max( match.Index + match.Length, group.Index + group.Length ) );

					inline = siblingRunBuilder.Build( left, span.ContentEnd );
					inline.Style( GroupSiblingValueStyleInfo );

					value_inline = runBuilder.Build( middle, span.ContentEnd );
					value_inline.Style( GroupValueStyleInfo, highlightStyle );

					inline = siblingRunBuilder.Build( right, span.ContentEnd );
					inline.Style( GroupSiblingValueStyleInfo );
				}
				inline = new Run( $"\x200E  （{capture.Index}, {capture.Length}）", span.ContentEnd );
				inline.Style( MatchNormalStyleInfo, LocationStyleInfo );

				para.Inlines.Add( span );
				_ = new LineBreak( span.ElementEnd ); // (after span)

				var capture_info = new CaptureInfo
				{
					Parent = groupInfo,
					CaptureSegment = new Segment( capture.Index, capture.Length ),
					Span = span,
					ValueInline = value_inline
				};

				span.Tag = capture_info;

				groupInfo.CaptureInfos.Add( capture_info );
			}
		}


		static string AdjustString( string s )
		{
			// 21A9 ↩
			// 21B2 ↲
			// 21B5 ↵
			// 23CE ⏎

			// 00B7 ·
			// 2219 ∙
			// 22C5 ⋅
			// 23B5 ⎵
			// 2E31 ⸱ 
			// 2420 ␠
			// 2423 ␣

			// 2192 →
			// 21E2 ⇢
			// 21E5 ⇥
			// 2589 ▷
			// 25B6 ▶
			// 25B8 ▸ 
			// 25B9 ▹ 
			// 2B62 ⭢
			// 2B72 ⭲
			// 2B6C ⭬

			s = s
					.Replace( "\r", @"\r" )
					.Replace( "\n", @"\n" )
					.Replace( "\t", @"\t" )
					//.Replace( " ", "\u00B7" )
					; // TODO: add more 
			return s;
		}


		class RunBuilder
		{
			struct MyRun
			{
				public string text;
				public bool isSpecial;
			}

			readonly StringBuilder sb = new StringBuilder( );
			readonly List<MyRun> runs = new List<MyRun>( );
			readonly StyleInfo specialStyleInfo;
			bool isPreviousSpecial = false;


			public RunBuilder( StyleInfo specialStyleInfo )
			{
				this.specialStyleInfo = specialStyleInfo;
			}


			public Inline Build( string text, TextPointer at )
			{
				sb.Clear( );
				runs.Clear( );
				isPreviousSpecial = false;

				foreach( var c in text )
				{
					switch( c )
					{
					case '\r':
						AppendSpecial( @"\r" );
						continue;
					case '\n':
						AppendSpecial( @"\n" );
						continue;
					case '\t':
						AppendSpecial( @"\t" );
						continue;
					}

					switch( char.GetUnicodeCategory( c ) )
					{
					case UnicodeCategory.UppercaseLetter:
					case UnicodeCategory.LowercaseLetter:
					case UnicodeCategory.TitlecaseLetter:
					//case UnicodeCategory.ModifierLetter:
					case UnicodeCategory.OtherLetter:
					//case UnicodeCategory.NonSpacingMark:
					//case UnicodeCategory.SpacingCombiningMark:
					//case UnicodeCategory.EnclosingMark:
					case UnicodeCategory.DecimalDigitNumber:
					case UnicodeCategory.LetterNumber:
					case UnicodeCategory.OtherNumber:
					case UnicodeCategory.SpaceSeparator:
					//case UnicodeCategory.LineSeparator:
					//case UnicodeCategory.ParagraphSeparator:
					//case UnicodeCategory.Control:
					//case UnicodeCategory.Format:
					//case UnicodeCategory.Surrogate:
					//case UnicodeCategory.PrivateUse:
					case UnicodeCategory.ConnectorPunctuation:
					case UnicodeCategory.DashPunctuation:
					case UnicodeCategory.OpenPunctuation:
					case UnicodeCategory.ClosePunctuation:
					case UnicodeCategory.InitialQuotePunctuation:
					case UnicodeCategory.FinalQuotePunctuation:
					case UnicodeCategory.OtherPunctuation:
					case UnicodeCategory.MathSymbol:
					case UnicodeCategory.CurrencySymbol:
					//case UnicodeCategory.ModifierSymbol:
					case UnicodeCategory.OtherSymbol:
						//case UnicodeCategory.OtherNotAssigned:

						AppendNormal( c );

						break;

					default:

						AppendCode( c );

						break;
					}
				}

				// last
				if( sb.Length > 0 )
				{
					runs.Add( new MyRun { text = sb.ToString( ), isSpecial = isPreviousSpecial } );
				}

				// TODO: maybe insert element at position after creation.

				switch( runs.Count )
				{
				case 0:
					return new Span( (Inline)null, at );
				case 1:
				{
					var r = runs[0];
					var run = new Run( r.text, at );
					if( r.isSpecial )
					{
						Debug.Assert( specialStyleInfo != null );

						run.Style( specialStyleInfo );
					}
					return run;
				}
				default:
				{
					var r = runs[0];
					var run = new Run( r.text );
					if( r.isSpecial )
					{
						Debug.Assert( specialStyleInfo != null );

						run.Style( specialStyleInfo );
					}

					var span = new Span( run, at );

					for( int i = 1; i < runs.Count; ++i )
					{
						r = runs[i];
						run = new Run( r.text, span.ContentEnd );
						if( r.isSpecial )
						{
							Debug.Assert( specialStyleInfo != null );

							run.Style( specialStyleInfo );
						}
					}

					return span;
				}
				}
			}


			private void AppendSpecial( string s )
			{
				if( isPreviousSpecial || specialStyleInfo == null )
				{
					sb.Append( s );
				}
				else
				{
					Debug.Assert( specialStyleInfo != null );

					if( sb.Length > 0 )
					{
						runs.Add( new MyRun { text = sb.ToString( ), isSpecial = false } );
					}
					sb.Clear( ).Append( s );
					isPreviousSpecial = true;
				}
			}


			private void AppendCode( char c )
			{
				AppendSpecial( $@"\u{(int)c:D4}" );
			}


			private void AppendNormal( char c )
			{
				if( !isPreviousSpecial )
				{
					sb.Append( c );
				}
				else
				{
					Debug.Assert( specialStyleInfo != null );

					if( sb.Length > 0 )
					{
						runs.Add( new MyRun { text = sb.ToString( ), isSpecial = true } );
					}
					sb.Clear( ).Append( c );
					isPreviousSpecial = false;
				}
			}
		}


		List<Info> GetUnderliningInfos( CancellationToken ct )
		{
			List<Info> infos = new List<Info>( );

			TextSelection sel = rtbMatches.Selection;

			for( var parent = sel.Start.Parent; parent != null; )
			{
				if( ct.IsCancellationRequested ) return infos;

				object tag = null;

				switch( parent )
				{
				case FrameworkElement fe:
					tag = fe.Tag;
					parent = fe.Parent;
					break;
				case FrameworkContentElement fce:
					tag = fce.Tag;
					parent = fce.Parent;
					break;
				}

				switch( tag )
				{
				case MatchInfo mi:
					infos.Add( mi );
					return infos;
				case GroupInfo gi:
					infos.Add( gi );
					return infos;
				case CaptureInfo ci:
					infos.Add( ci );
					return infos;
				}
			}

			return infos;
		}


		void RestartExternalUnderline( IReadOnlyList<Segment> segments, bool setSelection )
		{
			UnderliningTask.RestartAfter( ShowMatchesTask, ct => ExternalUnderlineTaskProc( ct, segments, setSelection ) );
		}


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void ExternalUnderlineTaskProc( CancellationToken ct, IReadOnlyList<Segment> segments0, bool setSelection )
		{
			try
			{
				if( ct.WaitHandle.WaitOne( 333 ) ) return;
				ct.ThrowIfCancellationRequested( );

				var inlines_to_underline = new List<(Inline inline, Info info)>( );

				var segments = new HashSet<Segment>( segments0 );

				foreach( var mi in MatchInfos )
				{
					ct.ThrowIfCancellationRequested( );

					foreach( var gi in mi.GroupInfos )
					{
						ct.ThrowIfCancellationRequested( );

						if( segments.Contains( gi.GroupSegment ) )
						{
							inlines_to_underline.Add( (gi.ValueInline, gi) );
						}

						foreach( var ci in gi.CaptureInfos )
						{
							ct.ThrowIfCancellationRequested( );

							if( segments.Contains( ci.CaptureSegment ) )
							{
								inlines_to_underline.Add( (ci.ValueInline, ci) );
							}
						}
					}

					if( segments.Contains( mi.MatchSegment ) )
					{
						inlines_to_underline.Add( (mi.ValueInline, mi) );
					}
				}

				ct.ThrowIfCancellationRequested( );

				ChangeEventHelper.Invoke( ct, ( ) =>
				{
					UnderliningAdorner.SetRangesToUnderline(
						inlines_to_underline
							.Select( r => (r.inline.ContentStart, r.inline.ContentEnd) )
							.ToList( ) );

					inlines_to_underline.FirstOrDefault( ).info?.GetMatchInfo( ).Span.BringIntoView( );
				} );

				ChangeEventHelper.Invoke( ct, ( ) =>
				{
					var first = inlines_to_underline.FirstOrDefault( ).inline;

					first?.BringIntoView( );

					if( setSelection && !rtbMatches.IsKeyboardFocused )
					{
						if( first != null )
						{
							var p = first.ContentStart.GetInsertionPosition( LogicalDirection.Forward );
							rtbMatches.Selection.Select( p, p );
						}
					}
				} );
			}
			catch( OperationCanceledException exc ) // also 'TaskCanceledException'
			{
				Utilities.DbgSimpleLog( exc );

				// ignore
			}
			catch( Exception exc )
			{
				_ = exc;
				if( Debugger.IsAttached ) Debugger.Break( );
				throw;
			}
		}


		void RestartLocalUnderlining( bool yes )
		{
			UnderliningTask.RestartAfter( ShowMatchesTask, ct => LocalUnderlineTaskProc( ct, yes ) );
		}


		[SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void LocalUnderlineTaskProc( CancellationToken ct, bool yes )
		{
			try
			{
				if( ct.WaitHandle.WaitOne( 222 ) ) return;
				ct.ThrowIfCancellationRequested( );

				List<Info> infos = null;
				ChangeEventHelper.Invoke( ct, ( ) =>
				{
					infos = GetUnderliningInfos( ct );

				} );

				var inlines_to_underline = new List<Inline>( );

				if( yes )
				{
					foreach( var info in infos )
					{
						ct.ThrowIfCancellationRequested( );

						switch( info )
						{
						case MatchInfo mi:
							inlines_to_underline.Add( mi.ValueInline );
							break;
						case GroupInfo gi:
							if( gi.IsSuccess ) inlines_to_underline.Add( gi.ValueInline );
							break;
						case CaptureInfo ci:
							inlines_to_underline.Add( ci.ValueInline );
							break;
						}
					}
				}

				ct.ThrowIfCancellationRequested( );

				ChangeEventHelper.Invoke( ct, ( ) =>
				{
					UnderliningAdorner.SetRangesToUnderline(
						inlines_to_underline
							.Select( i => (i.ContentStart, i.ContentEnd) )
							.ToList( ) );
				} );
			}
			catch( OperationCanceledException exc ) // also 'TaskCanceledException'
			{
				Utilities.DbgSimpleLog( exc );

				// ignore
			}
			catch( Exception exc )
			{
				_ = exc;
				if( Debugger.IsAttached ) Debugger.Break( );
				throw;
			}
		}


		void ShowOne( RichTextBox rtb )
		{
			void setVisibility( RichTextBox rtb1 )
			{
				var v = rtb1 == rtb ? Visibility.Visible : Visibility.Hidden;
				rtb1.Visibility = v;
			}

			setVisibility( rtbMatches );
			setVisibility( rtbNoMatches );
			setVisibility( rtbNoPattern );
			setVisibility( rtbError );

			if( !rtbMatches.IsVisible )
			{
				ChangeEventHelper.Do( ( ) =>
				{
					secMatches.Blocks.Clear( );
				} );
			}

			if( !rtbError.IsVisible )
			{
				runError.Text = "";
			}

			pbProgress.Visibility = Visibility.Hidden;
		}


		[Conditional( "DEBUG" )]
		private void ShowDebugInformation( )
		{
			string s = "";

			TextPointer start = rtbMatches.Selection.Start;

			Rect rectB = start.GetCharacterRect( LogicalDirection.Backward );
			Rect rectF = start.GetCharacterRect( LogicalDirection.Forward );

			s += $"BPos: {(int)rectB.Left}×{(int)rectB.Bottom}, FPos: {(int)rectF.Left}×{(int)rectF.Bottom}";

			char[] bc = new char[1];
			char[] fc = new char[1];

			int bn = start.GetTextInRun( LogicalDirection.Backward, bc, 0, 1 );
			int fn = start.GetTextInRun( LogicalDirection.Forward, fc, 0, 1 );

			s += $", Bc: '{( bn == 0 ? '∅' : bc[0] )}', Fc: '{( fn == 0 ? '∅' : fc[0] )}";

			lblDbgInfo.Content = s;
		}


		private void btnDbgSave_Click( object sender, RoutedEventArgs e )
		{
#if DEBUG
			rtbMatches.Focus( );

			Utilities.DbgSaveXAML( @"debug-ucmatches.xml", rtbMatches.Document );

			//SaveToPng( Window.GetWindow( this ), "debug-ucmatches.png" );
#endif
		}


		private void btnDbgLoad_Click( object sender, RoutedEventArgs e )
		{
#if DEBUG
			rtbMatches.Focus( );

			Utilities.DbgLoadXAML( rtbMatches.Document, @"debug-ucmatches.xml" );
#endif
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

					using( ShowMatchesTask ) { }
					using( UnderliningTask ) { }
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~UCMatches()
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
