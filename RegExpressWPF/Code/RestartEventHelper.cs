using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RegExpressWPF.Code
{
	/// <summary>
	/// 
	/// </summary>
	public class RestartEvents : IDisposable
	{
		readonly AutoResetEvent StopEvent;
		readonly AutoResetEvent RestartEvent;


		public RestartEvents( )
		{
			StopEvent = new AutoResetEvent( initialState: false );
			RestartEvent = new AutoResetEvent( initialState: false );
		}


		public void SendStop( )
		{
			RestartEvent.Reset( );
			StopEvent.Set( );
		}


		public void SendRestart( )
		{
			RestartEvent.Set( );
		}


		public RestartEventsHelper BuildHelper( )
		{
			return new RestartEventsHelper( StopEvent, RestartEvent );
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

					using( StopEvent ) { }
					using( RestartEvent ) { }
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~StoppableRestartEvents()
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


	public interface ICancellable
	{
		bool IsCancelRequested { get; }
	}


	/// <summary>
	/// 
	/// </summary>
	public class RestartEventsHelper : ICancellable
	{
		public enum Status
		{
			None,
			Stop,
			Restart,
		}


		readonly AutoResetEvent[] Events;

		bool IsStopRequestDetected;
		bool IsRestartRequestDetected;


		public RestartEventsHelper( AutoResetEvent stopEvent, AutoResetEvent restartEvent )
		{
			Events = new[] { stopEvent, restartEvent };

			IsStopRequestDetected = false;
			IsRestartRequestDetected = false;
		}


		public Status WaitInfinite( )
		{
			IsStopRequestDetected = false;
			IsRestartRequestDetected = false;

			int n = WaitHandle.WaitAny( Events );
			switch( n )
			{
			case 0:
				IsStopRequestDetected = true;
				return Status.Stop;
			case 1:
				IsRestartRequestDetected = true;
				return Status.Restart;
			default:
				Debug.Assert( false );
				break;
			}

			Debug.Assert( false );

			return Status.None;
		}


		public Status WaitForSilence( int timeout1, int timeout2 )
		{
			if( IsStopRequestDetected ) return Status.Stop;

			IsRestartRequestDetected = false;

			for( int timeout = timeout1; ; timeout = timeout2 )
			{
				int n = WaitHandle.WaitAny( Events, timeout );

				switch( n )
				{
				case 0:
					IsStopRequestDetected = true;
					return Status.Stop;
				case 1:
					continue;
				case WaitHandle.WaitTimeout:
					return Status.None;
				default:
					Debug.Assert( false );
					break;
				}
			}
		}


		public Status GetStatus( )
		{
			if( IsStopRequestDetected ) return Status.Stop;
			if( IsRestartRequestDetected ) return Status.Restart;

			int n = WaitHandle.WaitAny( Events, 0 );

			switch( n )
			{
			case 0:
				IsStopRequestDetected = true;
				return Status.Stop;
			case 1:
				IsRestartRequestDetected = true;
				return Status.Restart;
			case WaitHandle.WaitTimeout:
				break;
			default:
				Debug.Assert( false );
				break;
			}

			return Status.None;
		}


		public bool IsStopRequested
		{
			get
			{
				return GetStatus( ) == Status.Stop;
			}
		}


		public bool IsRestartRequested
		{
			get
			{
				return GetStatus( ) == Status.Restart;
			}
		}


		public bool IsAnyRequested
		{
			get
			{
				return GetStatus( ) != Status.None;
			}
		}


		public static ICancellable NonCancellable
		{
			get { return NonCancellableS.Instance; }
		}

		sealed class NonCancellableS : ICancellable
		{
			internal static readonly NonCancellableS Instance = new NonCancellableS( );

			public bool IsCancelRequested => false;
		}



		#region ICancellable

		public bool IsCancelRequested => IsAnyRequested;

		#endregion ICancellable
	}
}
