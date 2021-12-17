using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;
using Quaestor.Utilities;

namespace Quaestor.MiniCluster
{
	public class ClusterHeartBeat
	{
		private readonly CancellationTokenSource
			_cancellationSource = new CancellationTokenSource();

		private readonly Cluster _cluster;
		private readonly ILogger _logger = Log.CreateLogger<ClusterHeartBeat>();

		[NotNull] private readonly Func<IManagedProcess, Task<bool>> _unavailableProcedure;
		[NotNull] private readonly Func<IManagedProcess, Task<bool>> _unhealthyProcedure;

		public ClusterHeartBeat([NotNull] Cluster cluster,
		                        [NotNull] Func<IManagedProcess, Task<bool>> unhealthyProcedure,
		                        [NotNull] Func<IManagedProcess, Task<bool>> unavailableProcedure)
		{
			_cluster = cluster;
			_unhealthyProcedure = unhealthyProcedure;
			_unavailableProcedure = unavailableProcedure;
		}

		public Task StartAsync()
		{
			_logger.LogInformation($"Starting heartbeat for cluster {_cluster.Name}...");

			Task.Run(HeartBeatLoop);

			return Task.CompletedTask;
		}

		public Task ShutdownAsync()
		{
			_cancellationSource.Cancel();
			return Task.CompletedTask;
		}

		public async Task<bool> CheckHeartBeatAsync(IManagedProcess member)
		{
			var timeout = _cluster.MemberResponseTimeOut;

			try
			{
				Stopwatch watch = Stopwatch.StartNew();

				// Check for health first, it might be that the process is running but we have not
				// started it.
				var healthy = await TaskUtils.TimeoutAfter(member.IsServingAsync(), timeout);

				if (healthy)
				{
					_logger.LogInformation(
						"Heartbeat detected for {member}. All services are healthy [{milliseconds}ms].",
						member, watch.ElapsedMilliseconds);

					return true;
				}

				if (member.IsKnownRunning)
				{
					_logger.LogWarning(
						"Process is running for {member} but some services are un-healthy [{milliseconds}ms].",
						member, watch.ElapsedMilliseconds);

					return await _unhealthyProcedure.Invoke(member);
				}

				_logger.LogWarning("Cluster member not running ({member}) [{milliseconds}ms].",
					member, watch.ElapsedMilliseconds);

				return await _unavailableProcedure.Invoke(member);
			}
			catch (TimeoutException)
			{
				_logger.LogWarning("Heartbeat timed out for {member}.", member);

				return await _unavailableProcedure.Invoke(member);
			}
			catch (RpcException rpcException)
			{
				_logger.LogWarning(rpcException,
					"RPC error in heartbeat detection for {member}: {exceptionMessage}",
					member, rpcException.Message);

				return await _unavailableProcedure.Invoke(member);
			}
			catch (Exception e)
			{
				_logger.LogError(e, "Error in heartbeat detection for {member}: {exceptionMessage}",
					member, e.Message);

				return false;
			}
		}

		private async Task HeartBeatLoop()
		{
			await Task.Yield();

			while (!_cancellationSource.IsCancellationRequested)
			{
				try
				{
					_logger.LogInformation("-------------Heartbeat [{time}]------------------",
						DateTime.Now);

					var members = _cluster.Members;

					foreach (var member in members.ToList())
					{
						// This could be done in parallel. However, we're not in a hurry and the logs would get messy
						if (member.MonitoringSuspended)
						{
							_logger.LogInformation(
								$"Monitoring is suspended for {member} (probably shutting down).");

							continue;
						}

						if (await CheckHeartBeatAsync(member))
						{
							if (member is IServerProcess serverProcess)
							{
								// Just to be save - in case they have been started previously.
								_cluster.ServiceRegistrar?.Ensure(serverProcess);
							}
						}
					}

					foreach (IManagedProcess managedProcess in members.ToList())
					{
						if (!managedProcess.IsDueForRecycling)
						{
							continue;
						}

						ServiceRegistrar serviceRegistrar = _cluster.ServiceRegistrar;

						await TryRecycle(managedProcess, serviceRegistrar);
					}

					await Task.Delay(_cluster.HeartBeatInterval);
				}
				catch (Exception exception)
				{
					_logger.LogError(exception, "Heartbeat loop failed");
				}
			}
		}

		private async Task<bool> TryRecycle([NotNull] IManagedProcess managedProcess,
		                                    [CanBeNull] ServiceRegistrar serviceRegistrar)
		{
			_logger.LogInformation("The process {process} is due for recycling.", managedProcess);

			int ongoingRequests = await managedProcess.GetOngoingRequestCountAsync();

			if (ongoingRequests > 0)
			{
				_logger.LogWarning(
					"Process recycling is delayed due to one or more ongoing requests being processed.");
				return false;
			}

			// TODO: Send shutdown signal (that sets unhealthy = true and then waits another few seconds before shutting down to avoid races)

			await ManagedProcessUtils.ShutDownAsync(managedProcess, serviceRegistrar,
				TimeSpan.Zero);

			// However, the advantage of killing is that we can re-start it straigh away:
			bool success = await managedProcess.StartAsync();

			if (success &&
			    managedProcess is IServerProcess serverProcess)
			{
				serviceRegistrar?.Ensure(serverProcess);
			}

			return success;
		}
	}
}
