using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace RegExpressWPF.Code
{
    public class TaskHelper
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
            Stop( );

            var ct =
                CancellationTokenSource.CreateLinkedTokenSource(
                    taskBefore.mCancelationTokenSource.Token,
                    mCancelationTokenSource.Token ).Token;

            taskBefore.mTask.ContinueWith( t => action( ct ), ct );
        }


        public void Stop( )
        {
            using( mCancelationTokenSource )
            {
                mCancelationTokenSource.Cancel( );

                try
                {
                    mTask.Wait( );
                }
                catch( OperationCanceledException )
                {
                    // ignore
                }
                catch( AggregateException exc )
                {
                    if( !exc.InnerExceptions.All( e => e is OperationCanceledException ) ) throw;

                    // ignore
                }
            }

            mCancelationTokenSource = new CancellationTokenSource( );
        }
    }
}
