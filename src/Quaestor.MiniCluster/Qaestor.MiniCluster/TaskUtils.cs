using System;
using System.Threading;
using System.Threading.Tasks;

namespace Quaestor.MiniCluster
{
	public static class TaskUtils
	{
		public static async Task<TResult> TimeoutAfter<TResult>(Task<TResult> task, TimeSpan timeout)
		{
			using (var timeoutCancellationTokenSource = new CancellationTokenSource())
			{
				var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));

				if (completedTask == task)
				{
					timeoutCancellationTokenSource.Cancel();
					return await task;
				}

				throw new TimeoutException("The operation has timed out.");
			}
		}
	}
}
