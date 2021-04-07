using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;

namespace Quaestor.MiniCluster
{
	public class ClusterHeartBeat
	{
		private readonly CancellationTokenSource _cancellationSource = new CancellationTokenSource();

		private readonly Cluster _cluster;
		private readonly ILogger _logger = Log.CreateLogger<ClusterHeartBeat>();
		private readonly Func<IManagedProcess, Task<bool>> _unavailableProcedure;

		private readonly Func<IManagedProcess, Task<bool>> _unhealthyProcedure;

		public ClusterHeartBeat(Cluster cluster,
		                        Func<IManagedProcess, Task<bool>> unhealthyProcedure,
		                        Func<IManagedProcess, Task<bool>> unavailableProcedure)
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

		private async Task HeartBeatLoop()
		{
			await Task.Yield();

			while (!_cancellationSource.IsCancellationRequested)
			{
				try
				{
					var members = _cluster.Members;

					foreach (var member in members)
					{
						if (member.MonitoringSuspended)
						{
							_logger.LogInformation($"Monitoring is suspended for {member} (probably shutting down).");

							continue;
						}

						await CheckHeartBeatAsync(member);
					}

					await Task.Delay(_cluster.HeartBeatInterval);
				}
				catch (Exception x)
				{
					_logger.LogError(x, "Heartbeat loop failed");
				}
			}
		}

		private async Task CheckHeartBeatAsync(IManagedProcess member)
		{
			var timeout = _cluster.MemberResponseTimeOut;

			try
			{
				if (!member.IsRunning)
				{
					_logger.LogWarning(
						"Cluster member not running ({member}).", member);
					_unavailableProcedure?.Invoke(member);
					return;
				}

				Stopwatch watch = Stopwatch.StartNew();

				var healthy = await TaskUtils.TimeoutAfter(member.IsServingAsync(), timeout);

				if (!healthy)
				{
					_logger.LogWarning(
						"Heartbeat detected for {member} but some services are un-healthy [{milliseconds}ms].",
						member, watch.ElapsedMilliseconds);

					_unhealthyProcedure?.Invoke(member);
				}
				else
				{
					_logger.LogInformation(
						"Heartbeat detected for {member}. All services are healthy [{milliseconds}ms].",
						member, watch.ElapsedMilliseconds);
				}
			}
			catch (TimeoutException)
			{
				_logger.LogWarning("Heartbeat timed out for {member}.", member);

				_unavailableProcedure?.Invoke(member);
			}
			catch (RpcException rpcException)
			{
				_logger.LogWarning(rpcException, "RPC error in heartbeat detection for {member}: {exceptionMessage}",
					member, rpcException.Message);
				_unavailableProcedure?.Invoke(member);
			}
			catch (Exception e)
			{
				_logger.LogError(e, "Error in heartbeat detection for {member}: {exceptionMessage}", member, e.Message);
			}
		}
	}
}
