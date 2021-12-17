using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Quaestor.MiniCluster
{
	internal static class ManagedProcessUtils
	{
		public static async Task<string> ShutDownAsync(
			[NotNull] IManagedProcess process,
			[CanBeNull] ServiceRegistrar serviceRegistrar,
			TimeSpan maxShutDownTime)
		{
			string message = null;
			if (process.IsKnownRunning)
			{
				process.MonitoringSuspended = true;
				try
				{
					if (process is IServerProcess serverProcess)
					{
						serviceRegistrar?.EnsureRemoved(serverProcess);
					}

					bool isShutDown = false;

					if (maxShutDownTime > TimeSpan.Zero)
					{
						isShutDown = await process.TryShutdownAsync(maxShutDownTime);
					}

					if (!isShutDown)
					{
						message =
							$"Process has not shut down within {maxShutDownTime.TotalSeconds}s. We had to kill it.";

						process.Kill();
					}
				}
				finally
				{
					process.MonitoringSuspended = false;
				}
			}

			return message;
		}
	}
}
