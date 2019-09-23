﻿using RegExpressWPF.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;


namespace RegExpressWPF.Code
{
    internal class UndoRedoHelper
    {
        class Diff
        {
            internal int Position;
            internal string Remove;
            internal string Add;

            public override string ToString( )
            {
                return $"At {Position}, Remove '{Remove}', Add '{Add}'";
            }
        }

        class SelectionInfo
        {
            internal readonly int SelectionStart;
            internal readonly int SelectionEnd;

            public SelectionInfo( int selectionStart, int selectionEnd )
            {
                SelectionStart = selectionStart;
                SelectionEnd = selectionEnd;
            }

            internal int Length => Math.Abs( SelectionStart - SelectionEnd );

            public override string ToString( )
            {
                return $"{SelectionStart}..{SelectionEnd}";
            }
        }

        class UndoItem
        {
            internal Diff Diff;
            internal SelectionInfo SelectionInfoA;
            internal SelectionInfo SelectionInfoB;
        }

        readonly MyRichTextBox Rtb;
        readonly List<UndoItem> UndoList = new List<UndoItem>( );
        readonly List<UndoItem> RedoList = new List<UndoItem>( );
        string PreviousText;
        SelectionInfo PreviousSelection = new SelectionInfo( 0, 0 );
        bool IsUndoOrRedo = false;
        bool IsTrackingTextChange = false;


        public UndoRedoHelper( MyRichTextBox rtb )
        {
            Rtb = rtb;
            Rtb.CommandBindings.Add( new CommandBinding( ApplicationCommands.Undo, HandleUndo ) );
            Rtb.CommandBindings.Add( new CommandBinding( ApplicationCommands.Redo, HandleRedo ) );

            Rtb.LostFocus += HandleLostFocus;

            Init( );
        }


        public void Init( )
        {
            var td = Rtb.GetTextData( null );

            PreviousText = td.Text;
            UndoList.Clear( );
            RedoList.Clear( );

            UndoList.Add( new UndoItem
            {
                Diff = GetDiff( "", td.Text ),
                SelectionInfoA = new SelectionInfo( 0, 0 ),
                SelectionInfoB = new SelectionInfo( td.SelectionStart, td.SelectionEnd )
            } );
        }


        public void HandleTextChanged( )
        {
            if( IsUndoOrRedo ) return;

            var td = Rtb.GetTextData( null );

            var si = new SelectionInfo( td.SelectionStart, td.SelectionEnd );

            var ui = new UndoItem
            {
                Diff = GetDiff( PreviousText, td.Text ),
                SelectionInfoA = PreviousSelection,
                SelectionInfoB = si,
            };

            // try combining
            bool combined = false;
            if( UndoList.Count > 1 ) // (exclude the first initial one)
            {
                var last = UndoList.Last( );
                if( IsTrackingTextChange && CanBeCombined( last, ui ) )
                {
                    last.Diff.Add += ui.Diff.Add;
                    last.SelectionInfoB = new SelectionInfo( td.SelectionStart, td.SelectionEnd );
                    combined = true;
                }
            }

            if( !combined ) UndoList.Add( ui );

            PreviousText = td.Text;
            PreviousSelection = si;

            RedoList.Clear( );

            IsTrackingTextChange = true;
        }


        public void HandleSelectionChanged( )
        {
            if( IsUndoOrRedo ) return;

            var td = Rtb.GetTextData( null );

            PreviousSelection = new SelectionInfo( td.SelectionStart, td.SelectionEnd );
        }


        public bool DoUndo( )
        {
            if( UndoList.Count < 2 ) return false;

            var last = UndoList.Last( );
            UndoList.RemoveAt( UndoList.Count - 1 );

            RedoList.Add( last );

            Debug.Assert( !IsUndoOrRedo );
            IsUndoOrRedo = true;

            try
            {
                var td = Rtb.GetTextData( null );

                using( Rtb.DeclareChangeBlock( ) )
                {
                    var range = td.Range( last.Diff.Position, last.Diff.Add.Length );
                    range.Text = Regex.Replace( last.Diff.Remove, @"\r\n|\n", "\r" ); // (it does not like '\n')
                    range.ClearAllProperties( );

                    td = Rtb.GetTextData( null );
                    Rtb.Selection.Select( td.Pointers[last.SelectionInfoA.SelectionStart], td.Pointers[Math.Min( last.SelectionInfoA.SelectionEnd, td.Pointers.Count - 1 )] );
                }

                PreviousText = td.Text;
                PreviousSelection = new SelectionInfo( td.SelectionStart, td.SelectionEnd );// last.SelectionInfoA;

                IsTrackingTextChange = false;

                return true;
            }
            finally
            {
                Debug.Assert( IsUndoOrRedo );
                IsUndoOrRedo = false;
            }
        }


        public bool DoRedo( )
        {
            if( !RedoList.Any( ) ) return false;

            var last = RedoList.Last( );
            RedoList.RemoveAt( RedoList.Count - 1 );

            UndoList.Add( last );

            Debug.Assert( !IsUndoOrRedo );
            IsUndoOrRedo = true;

            try
            {
                var td = Rtb.GetTextData( null );

                using( Rtb.DeclareChangeBlock( ) )
                {
                    var range = td.Range( last.Diff.Position, last.Diff.Remove.Length );
                    range.Text = Regex.Replace( last.Diff.Add, @"\r\n|\n", "\r" ); // (it does not like '\n')
                    range.ClearAllProperties( );

                    td = Rtb.GetTextData( null );
                    Rtb.Selection.Select( td.Pointers[Math.Min( last.SelectionInfoB.SelectionStart, td.Pointers.Count - 1 )], td.Pointers[Math.Min( last.SelectionInfoB.SelectionEnd, td.Pointers.Count - 1 )] );
                }

                PreviousText = td.Text;
                PreviousSelection = new SelectionInfo( td.SelectionStart, td.SelectionEnd );// 

                IsTrackingTextChange = false;

                return true;
            }
            finally
            {
                Debug.Assert( IsUndoOrRedo );
                IsUndoOrRedo = false;
            }
        }


        void HandleLostFocus( object sender, RoutedEventArgs e )
        {
            IsTrackingTextChange = false;
        }


        void HandleUndo( object sender, ExecutedRoutedEventArgs e )
        {
            DoUndo( );
        }


        void HandleRedo( object sender, ExecutedRoutedEventArgs e )
        {
            DoRedo( );
        }


        static Diff GetDiff( string first, string second )
        {
            first = first ?? string.Empty;
            second = second ?? string.Empty;

            int i = 0;
            while( i < first.Length && i < second.Length && first[i] == second[i] ) ++i;

            int j1 = first.Length - 1;
            int j2 = second.Length - 1;
            while( j1 >= i && j2 >= i && first[j1] == second[j2] ) { --j1; --j2; }

            return new Diff
            {
                Position = i,
                Remove = first.Substring( i, j1 - i + 1 ),
                Add = second.Substring( i, j2 - i + 1 )
            };
        }


        static bool CanBeCombined( UndoItem ui1, UndoItem ui2 )
        {
            return
                string.IsNullOrEmpty( ui2.Diff.Remove ) &&
                ui1.SelectionInfoB.Length == 0 &&
                ui2.SelectionInfoA.Length == 0 &&
                ui1.SelectionInfoB.SelectionStart == ui2.SelectionInfoA.SelectionStart;
        }


        /*
        static string Undo( string s, Diff d )
        {
            return s.Remove( d.Position, d.Add.Length ).Insert( d.Position, d.Remove );
        }


        static string Redo( string s, Diff d )
        {
            return s.Remove( d.Position, d.Remove.Length ).Insert( d.Position, d.Add );
        }
        */
    }
}
