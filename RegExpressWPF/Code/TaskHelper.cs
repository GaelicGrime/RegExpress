using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace RegExpressWPF.Code
{
	public sealed class TaskHelper : IDisposable
	{
		CancellationTokenSource mCancelationTokenSource = new CancellationTokenSource( );
		Task mTask = Task.CompletedTask;


		public void Restart( Action<CancellationToken> action )
		{
			Stop( );

			mTask = Task.Run( ( ) => action( mCancelationTokenSource.Token ), mCancelationTokenSource.Token );
		}


		public void RestartAfter( TaskHelper taskBefore, Action<CancellationToken> action )
		{
			Debug.Assert( this != taskBefore );

			Stop( );

			var ts =
				CancellationTokenSource.CreateLinkedTokenSource(
					taskBefore.mCancelationTokenSource.Token,
					mCancelationTokenSource.Token );

			var ct = ts.Token;

			taskBefore.mTask
				.ContinueWith( _ => action( ct ), ct, TaskContinuationOptions.NotOnCanceled | TaskContinuationOptions.NotOnFaulted, TaskScheduler.Default )
				.ContinueWith( _ => { ts.Dispose( ); return Task.CompletedTask; } );
		}


		public void Cancel( )
		{
			mCancelationTokenSource.Cancel( );
		}


		[System.Diagnostics.CodeAnalysis.SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		public void Stop( )
		{
			using( mCancelationTokenSource )
			{
				mCancelationTokenSource.Cancel( );

				try
				{
					mTask.Wait( );
				}
				catch( OperationCanceledException exc )
				{
					Utilities.DbgSimpleLog( exc );

					// ignore
				}
				catch( AggregateException exc )
				{
					if( !exc.InnerExceptions.All( e => e is OperationCanceledException ) )
					{
						if( Debugger.IsAttached ) Debugger.Break( );
						throw;
					}

					// ignore
				}
			}

			mCancelationTokenSource = new CancellationTokenSource( );
		}

		#region IDisposable Support

		private bool disposedValue = false; // To detect redundant calls

		/*protected virtual*/
		void Dispose( bool disposing )
		{
			if( !disposedValue )
			{
				if( disposing )
				{
					// TODO: dispose managed state (managed objects).

					using( mCancelationTokenSource ) { }
					using( mTask ) { }

					mCancelationTokenSource = null;
					mTask = null;
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~TaskHelper()
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
