using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Quaestor.Environment;
using Quaestor.KeyValueStore;
using Quaestor.Utilities;

namespace Quaestor.MiniCluster
{
	public class AgentGuardianService : BackgroundService
	{
		private readonly ILogger<AgentGuardianService> _logger =
			Log.CreateLogger<AgentGuardianService>();

		private ClusterConfig _clusterConfig;

		private Cluster _cluster;

		private static readonly Random _random = new Random();

		public AgentGuardianService(IConfiguration configuration)
		{
			_clusterConfig = new ClusterConfig();

			ConfigureEnvironment(configuration);

			// This is often fired twice in fast succession!
			ChangeToken.OnChange(
				configuration.GetReloadToken,
				ConfigureEnvironment, configuration);
		}

		private void ConfigureEnvironment(IConfiguration configuration)
		{
			_logger.LogDebug("Configuring cluster agents...");

			KnownAgents.ConfigureAgents(configuration);

			if (_clusterConfig == null)
			{
				_clusterConfig = new ClusterConfig();
			}

			configuration.GetSection(nameof(ClusterConfig)).Bind(_clusterConfig);

			if (_cluster != null)
			{
				ConfigureClusterAgents();
			}
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await Task.CompletedTask;
		}

		public override async Task StartAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Service Starting");

			if (KnownAgents.AgentConfigurations.Count == 0)
			{
				_logger.LogWarning("No cluster agents configured.");
				return;
			}

			var serviceRegistry =
				new ServiceRegistry(new LocalKeyValueStore(), _clusterConfig.Name);

			_cluster = new Cluster(_clusterConfig)
			{
				ServiceRegistrar = new ServiceRegistrar(serviceRegistry)
			};

			ConfigureClusterAgents();

			_ = await _cluster.StartAsync();

			await base.StartAsync(cancellationToken);
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Stopping Service");

			bool result = _cluster != null &&
			              await _cluster.ShutdownAsync(new TimeSpan(0, 0, 5));

			_logger.LogInformation("All agents have been stopped: {result}", result);

			await base.StopAsync(cancellationToken);
		}

		public override void Dispose()
		{
			_logger.LogInformation("Disposing Service");

			base.Dispose();
		}

		private void ConfigureClusterAgents()
		{
			_cluster.ClearMembers();

			foreach (AgentConfiguration agentConfiguration in KnownAgents.AgentConfigurations)
			{
				var managedProcesses = GetManagedProcesses(agentConfiguration,
					_clusterConfig.MemberRecyclingIntervalHours);

				_cluster.AddRange(managedProcesses);
			}
		}

		private IEnumerable<LocalProcess> GetManagedProcesses(
			AgentConfiguration agentConfiguration,
			double averageRecyclingIntervalHours)
		{
			if (agentConfiguration.Ports == null || agentConfiguration.Ports.Count == 0)
			{
				// No ports specified, use ephemeral ports
				agentConfiguration.Ports =
					new List<int>(Enumerable.Repeat(-1, agentConfiguration.ProcessCount));
			}

			if (agentConfiguration.Ports.Count != agentConfiguration.ProcessCount)
			{
				throw new ArgumentException(
					"Number of ports must correspond to process count or be zero for ephemeral port usage");
			}

			for (int i = 0; i < agentConfiguration.ProcessCount; i++)
			{
				string hostName = agentConfiguration.HostName;
				int port = agentConfiguration.Ports[i];

				ChannelCredentials credentials =
					GrpcUtils.CreateChannelCredentials(agentConfiguration.UseTls,
						agentConfiguration.ClientCertificate);

				var managedProcess = new LocalProcess(agentConfiguration.AgentType,
					agentConfiguration.ExecutablePath, agentConfiguration.CommandLineArguments,
					hostName, port, credentials)
				{
					EnvironmentVariables = agentConfiguration.EnvironmentVariables,
					ClusterShutdownAction = agentConfiguration.ClusterShutdownAction,
					RecyclingIntervalHours = GetRandomPlusMinus(averageRecyclingIntervalHours, 10)
				};

				if (agentConfiguration.ServiceNames != null)
				{
					foreach (string serviceName in agentConfiguration.ServiceNames)
					{
						managedProcess.ServiceNames.Add(serviceName);
					}
				}

				_logger.LogDebug(
					"Agent has been configured with recycling interval {recyclingHours}. Process details: {process}",
					managedProcess.RecyclingIntervalHours, managedProcess);

				yield return managedProcess;
			}
		}

		/// <summary>
		///     Return a random number dispersed around the specified average. This is NOT a gaussian distribution!
		/// </summary>
		/// <param name="average"></param>
		/// <param name="plusMinusPercent"></param>
		/// <returns></returns>
		/// <exception cref="NotImplementedException"></exception>
		private static double GetRandomPlusMinus(double average, double plusMinusPercent)
		{
			double variation = _random.NextDouble() * plusMinusPercent * 2 - plusMinusPercent;

			return average + variation;
		}
	}
}
