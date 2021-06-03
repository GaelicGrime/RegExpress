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
	internal class ResumableLoop2
	{
		enum Command : int
		{
			Rewind,
			WaitAndExecute,
			Execute,
			Terminate,
		}


		class Cancellable : ICancellable2, IDisposable
		{
			readonly ManualResetEvent mEvent = new ManualResetEvent( false );
			bool mIsCancellationRequested = false;

			#region ICancellable2

			public bool IsCancellationRequested
			{
				get
				{
					return mIsCancellationRequested || ( mIsCancellationRequested = mEvent.WaitOne( 0 ) );
				}
			}

			public WaitHandle WaitHandle
			{
				get
				{
					return mEvent;
				}
			}

			#endregion ICancellable2


			#region IDisposable

			private bool disposedValue;

			protected virtual void Dispose( bool disposing )
			{
				if( !disposedValue )
				{
					if( disposing )
					{
						// TODO: dispose managed state (managed objects)

						mEvent.Dispose( );
					}

					// TODO: free unmanaged resources (unmanaged objects) and override finalizer
					// TODO: set large fields to null
					disposedValue = true;
				}
			}

			// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
			// ~Cancellable()
			// {
			//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			//     Dispose(disposing: false);
			// }

			public void Dispose( )
			{
				// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
				Dispose( disposing: true );
				GC.SuppressFinalize( this );
			}

			#endregion IDisposable

		}


		Command mCommand = Command.Rewind;
		readonly AutoResetEvent mCommandEvent = new AutoResetEvent( false );
		readonly int[] mTimeouts = new int[3];
		readonly Action<ICancellable2> mAction;
		readonly Thread mThread;


		internal ResumableLoop2( Action<ICancellable2> action, int timeout1, int timeout2 = 0, int timeout3 = 0 )
		{
			Debug.Assert( action != null );
			Debug.Assert( timeout1 > 0 );

			mAction = action;

			if( timeout2 <= 0 ) timeout2 = timeout1;
			if( timeout3 <= 0 ) timeout3 = timeout2;

			mTimeouts[0] = timeout1;
			mTimeouts[1] = timeout2;
			mTimeouts[2] = timeout3;

			mThread = new Thread( ThreadProc )
			{
				IsBackground = true,
				Priority = ThreadPriority.BelowNormal,
				Name = nameof( ResumableLoop2 )
			};

			mThread.Start( );
		}


		public void Rewind( )
		{
			SetCommand( Command.Rewind );
		}


		public void WaitAndExecute( )
		{
			SetCommand( Command.WaitAndExecute );
		}


		public void Execute( )
		{
			SetCommand( Command.Execute );
		}


		public bool Terminate( int timeoutMs = 333 )
		{
			SetCommand( Command.Terminate );

			return mThread.Join( timeoutMs );
		}


		// ---


		void SetCommand( Command command )
		{
			if( mCommand != Command.Terminate )
			{
				mCommand = command;
			}
			mCommandEvent.Set( );
		}


		private void ThreadProc( )
		{
			try
			{
				for(; ; )
				{
					mCommandEvent.WaitOne( );

					// Note: we just read the last command that was stored; previous commands will be lost

					Command command = mCommand;

					if( command == Command.Terminate ) break;
					if( command == Command.Rewind ) continue;

					if( command == Command.WaitAndExecute )
					{
						for( int i = 0; ; i = Math.Min( i + 1, mTimeouts.Length - 1 ) )
						{
							bool w = mCommandEvent.WaitOne( mTimeouts[i] );

							if( !w )
							{
								// timeout, no other commands; go to execution

								command = Command.Execute;

								break;
							}
							else
							{
								command = mCommand;
							}

							if( command == Command.WaitAndExecute ) continue; // (using next timeout)

							// other commands break this loop
							break;
						}

						if( command == Command.Terminate ) break;
						if( command == Command.Rewind ) continue;
					}

					Debug.Assert( command == Command.Execute );


					try
					{
						mAction( new Cancellable( ) );
					}
					catch( Exception exc )
					{
						_ = exc;
						if( Debugger.IsAttached ) Debugger.Break( );

						//...............
						throw; // TODO: maybe restart the loop?
					}


				}
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
