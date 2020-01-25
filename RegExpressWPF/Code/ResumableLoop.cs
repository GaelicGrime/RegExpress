using RegexEngineInfrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace RegExpressWPF.Code
{
	public sealed class ResumableLoop : ICancellable, IDisposable
	{
		enum Status
		{
			None,
			Stop,
			Restart,
		}

		readonly AutoResetEvent StopEvent = new AutoResetEvent( initialState: false );
		readonly AutoResetEvent RestartEvent = new AutoResetEvent( initialState: false );
		readonly AutoResetEvent[] Events;

		bool IsStopRequestDetected;
		bool IsRestartRequestDetected;

		Thread TheThread = null;


		public ResumableLoop( Action<ICancellable> action, int timeout1, int timeout2 = 0, int timeout3 = 0 )
		{
			if( timeout1 <= 0 ) throw new ArgumentException( "Invalid timeout: " + timeout1 );

			if( timeout2 <= 0 ) timeout2 = timeout1;
			if( timeout3 <= 0 ) timeout3 = timeout2;

			Events = new[] { StopEvent, RestartEvent };

			StartWorker( action, timeout1, timeout2, timeout3 );
		}


		public void SendStop( )
		{
			RestartEvent.Reset( ); //?
			StopEvent.Set( );
		}


		public void SendRestart( )
		{
			RestartEvent.Set( );
		}


		public ThreadPriority Priority
		{
			set
			{
				TheThread.Priority = value;
			}
		}


		void StartWorker( Action<ICancellable> action, int timeout1, int timeout2, int timeout3 )
		{
			if( TheThread != null ) throw new InvalidOperationException( "Thread already started." );

			TheThread = new Thread( ( ) => ThreadProc( action, timeout1, timeout2, timeout3 ) )
			{
				IsBackground = true,
				Priority = ThreadPriority.BelowNormal,
			};

			TheThread.Start( );
		}


		Status GetStatus( int timeoutMs )
		{
			if( IsStopRequestDetected ) return Status.Stop;

			if( IsRestartRequestDetected )
			{
				if( StopEvent.WaitOne( 0 ) ) // since it has priority
				{
					IsStopRequestDetected = true;

					return Status.Stop;
				}

				return Status.Restart;
			}

			int n = WaitHandle.WaitAny( Events, timeoutMs );

			switch( n )
			{
			case 0:
				IsStopRequestDetected = true;
				return Status.Stop;
			case 1:
				IsRestartRequestDetected = true;
				return Status.Restart;
			case WaitHandle.WaitTimeout:
				return Status.None;
			default:
				Debug.Assert( false );
				break;
			}

			return Status.None;
		}


		[System.Diagnostics.CodeAnalysis.SuppressMessage( "Design", "CA1031:Do not catch general exception types", Justification = "<Pending>" )]
		void ThreadProc( Action<ICancellable> action, int timeout1, int timeout2, int timeout3 )
		{
			Debug.Assert( timeout1 > 0 );
			Debug.Assert( timeout2 > 0 );
			Debug.Assert( timeout3 > 0 );

			int[] timeouts = new[] { timeout1, timeout2, timeout3 };

			IsRestartRequestDetected = false;

			try
			{
				for(; ; )
				{
					IsStopRequestDetected = false;

					var status = GetStatus( -1 );

					if( status == Status.Stop ) continue;
					if( status != Status.Restart ) { Debug.Assert( false ); continue; }

					Debug.Assert( status == Status.Restart );
					Debug.Assert( !IsStopRequestDetected );
					Debug.Assert( IsRestartRequestDetected );

					// wait for "silience"

					for( var i = 0; ; i = Math.Min( i + 1, timeouts.Length - 1 ) )
					{
						IsRestartRequestDetected = false;

						status = GetStatus( timeouts[i] );

						if( status == Status.Stop ) break;
						if( status == Status.Restart ) continue;
						if( status == Status.None ) break; // (i.e. timeout, "silence")
						Debug.Assert( false );
					}

					Debug.Assert( !IsRestartRequestDetected );

					if( status == Status.Stop ) continue;

					Debug.Assert( status == Status.None );

					try
					{
						action( this ); //
					}
					catch( OperationCanceledException ) // also 'TaskCanceledException'
					{
						IsStopRequestDetected = true; //?
					}
					catch( Exception exc )
					{
						_ = exc;
						if( Debugger.IsAttached ) Debugger.Break( );

						throw; // TODO: maybe restart the loop?
					}
				}
			}
			catch( ThreadInterruptedException )
			{
				// ignore
			}
			catch( ThreadAbortException )
			{
				// ignore
			}
			catch( Exception exc )
			{
				_ = exc;
				if( Debugger.IsAttached ) Debugger.Break( );
				throw;
			}
		}


		#region ICancellable
		public bool IsCancellationRequested
		{
			get
			{
				return GetStatus( 0 ) != Status.None;
			}
		}
		#endregion ICancellable

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		void Dispose( bool disposing )
		{
			if( !disposedValue )
			{
				if( disposing )
				{
					// TODO: dispose managed state (managed objects).

					using( StopEvent ) { }
					using( RestartEvent ) { }
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~ResumableLoop()
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


	sealed class NonCancellable : ICancellable
	{
		public static readonly ICancellable Instance = new NonCancellable( );

		private NonCancellable( )
		{

		}

		#region ICancellable
		public bool IsCancellationRequested
		{
			get
			{
				return false;
			}
		}
		#endregion ICancellable
	}
}
