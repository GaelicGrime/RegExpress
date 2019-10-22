using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace RegExpressWPF.Code
{
	// See: https://www.codeproject.com/articles/692963/how-to-get-rid-of-dispatcher-invoke, with adjustments

	class UITaskHelper
	{
		static TaskScheduler taskScheduler = null;


		/// <summary>
		/// Must be called (once) on UI thread.
		/// </summary>
		public static void Init( )
		{
			if( taskScheduler == null ) taskScheduler = TaskScheduler.FromCurrentSynchronizationContext( );
		}


		public static bool DbgIsInitialised
		{
			get
			{
				return taskScheduler != null;
			}
		}


		/// <summary>
		/// Invoke action on UI thread. Should not be called from UI thread.
		/// </summary>
		/// <param name="ct"></param>
		/// <param name="action"></param>
		[Obsolete( "", true )]
		public static void Invoke( CancellationToken ct, Action action )
		{
			Debug.Assert( taskScheduler != null );

			ct.ThrowIfCancellationRequested( );

			try
			{
				var task = Task.Factory.StartNew( ( ) => Execute( action ), ct, TaskCreationOptions.None, taskScheduler );

				if( task.IsFaulted ) throw new AggregateException( task.Exception );

				task.Wait( ct );

				if( task.IsFaulted ) throw new AggregateException( task.Exception );
			}
			catch( OperationCanceledException ) // also 'TaskCanceledException'
			{
				throw;
			}
			catch( Exception exc )
			{
				_ = exc;
				if( Debugger.IsAttached ) Debugger.Break( );
				throw;
			}
		}


		public static void Invoke( DispatcherObject obj, CancellationToken ct, Action action )
		{
			obj.Dispatcher.Invoke(
				( ) => Execute( action ),
				DispatcherPriority.Background,
				ct );
		}



		/// <summary>
		/// Begin an action on UI thread.
		/// Use 'task.Wait()' to wait for termination.
		/// </summary>
		/// <param name="ct"></param>
		/// <param name="action"></param>
		/// <returns></returns>
		[Obsolete( "", true )]
		public static Task BeginInvoke( CancellationToken ct, Action action )
		{
			Debug.Assert( taskScheduler != null );

			ct.ThrowIfCancellationRequested( );

			try
			{
				var task = Task.Factory.StartNew( ( ) => Execute( action ), ct, TaskCreationOptions.None, taskScheduler );

				if( task.IsFaulted ) throw new AggregateException( task.Exception );

				return task;
			}
			catch( OperationCanceledException ) // also 'TaskCanceledException'
			{
				throw;
			}
			catch( Exception exc )
			{
				_ = exc;
				if( Debugger.IsAttached ) Debugger.Break( );
				throw;
			}
		}


		public static Task BeginInvoke( DispatcherObject obj, CancellationToken ct, Action action )
		{
			return obj.Dispatcher.InvokeAsync(
				( ) => Execute( action ),
				DispatcherPriority.Background,
				ct ).Task;
		}


		[Obsolete( "", true )]
		public static Task ContinueWith( Task previousTask, CancellationToken ct, Action action )
		{
			Debug.Assert( taskScheduler != null );

			ct.ThrowIfCancellationRequested( );

			var task = previousTask.ContinueWith(
				( t ) => Execute( action ),
				TaskContinuationOptions.NotOnFaulted | TaskContinuationOptions.NotOnCanceled
				);

			if( task.IsFaulted ) throw new AggregateException( task.Exception );

			return task;
		}


		static void Execute( Action action )
		{
			try
			{
				action( );
			}
			catch( OperationCanceledException exc ) // also 'TaskCanceledException'
			{
				Utilities.DbgSimpleLog( exc );

				// ignore?
				throw;//.............
			}
			catch( Exception exc )
			{
				_ = exc;
				if( Debugger.IsAttached ) Debugger.Break( );
				throw;
			}
		}
	}
}
