using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Quaestor.MiniCluster.Guardian
{
	public class GuardianService : BackgroundService
	{
		private readonly ILogger<GuardianService> _logger;

		private readonly List<AgentConfiguration> _agentConfigurations;
		private readonly ClusterConfig _clusterConfig;

		private Cluster _cluster;

		public GuardianService(ILogger<GuardianService> logger, IConfiguration configuration)
		{
			_logger = logger;

			_agentConfigurations = configuration
				.GetSection(nameof(AgentConfiguration)).Get<List<AgentConfiguration>>();

			_clusterConfig = new ClusterConfig();
			configuration.GetSection(nameof(ClusterConfig)).Bind(_clusterConfig);
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await Task.CompletedTask;
		}

		public override Task StartAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Service Starting");

			if (_agentConfigurations == null || _agentConfigurations.Count == 0)
			{
				_logger.LogWarning("No cluster agents configured.");
				return Task.CompletedTask;
			}

			_cluster = new Cluster(_clusterConfig);

			foreach (AgentConfiguration agentConfiguration in _agentConfigurations)
			{
				var managedProcesses = GetManagedProcesses(agentConfiguration);

				_cluster.AddRange(managedProcesses);
			}

			_cluster.Start();

			return base.StartAsync(cancellationToken);
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Stopping Service");

			_cluster?.Abort();

			await base.StopAsync(cancellationToken);
		}

		public override void Dispose()
		{
			_logger.LogInformation("Disposing Service");

			base.Dispose();
		}

		private static IEnumerable<LocalProcess> GetManagedProcesses(AgentConfiguration agentConfiguration)
		{
			if (agentConfiguration.Ports == null || agentConfiguration.Ports.Count == 0)
			{
				// No ports specified, use ephemeral ports
				agentConfiguration.Ports = new List<int>(Enumerable.Repeat(-1, agentConfiguration.ProcessCount));
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

				var managedProcess = new LocalProcess(hostName, port);

				managedProcess.AgentType = agentConfiguration.AgentType;
				managedProcess.ExecutablePath = agentConfiguration.ExecutablePath;

				managedProcess.CommandLineArguments = agentConfiguration.CommandLineArguments;

				foreach (string serviceName in agentConfiguration.ServiceNames)
				{
					managedProcess.ServiceNames.Add(serviceName);
				}

				yield return managedProcess;
			}
		}
	}
}
