using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RegExpressWPF.Code
{
	/// <summary>
	///  
	/// </summary>
	/// <remarks>For local usage only.</remarks>
	public class RestartEventHelper
	{
		readonly AutoResetEvent Event;
		bool IsRestartRequestDetected;

		public RestartEventHelper( AutoResetEvent ev )
		{
			Event = ev;
			IsRestartRequestDetected = false;
		}


		public void WaitInfinite( )
		{
			IsRestartRequestDetected = false;
			Event.WaitOne( Timeout.Infinite );
		}


		public void WaitForSilence( int timeout1, int timeout2 )
		{
			IsRestartRequestDetected = false;
			int timeout = timeout1;
			while( Event.WaitOne( timeout ) ) { timeout = timeout2; }
		}


		public bool IsRestartRequested
		{
			get
			{
				return IsRestartRequestDetected || ( IsRestartRequestDetected = Event.WaitOne( 0 ) );
			}
		}
	}
}
