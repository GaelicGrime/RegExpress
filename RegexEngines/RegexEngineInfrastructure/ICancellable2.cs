using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace RegexEngineInfrastructure
{
	public interface ICancellable2
	{
		bool IsCancellationRequested { get; }
		WaitHandle WaitHandle { get; }
	}


	public sealed class NonCancellable2 : ICancellable2
	{
		public static readonly ICancellable2 Instance = new NonCancellable2( );

		static readonly ManualResetEvent AlwaysUnsetEvent = new ManualResetEvent( false );

		private NonCancellable2( )
		{

		}

		#region ICancellable2

		public bool IsCancellationRequested
		{
			get
			{
				return false;
			}
		}


		public WaitHandle WaitHandle
		{
			get
			{
				return AlwaysUnsetEvent;
			}
		}

		#endregion ICancellable2
	}
}
