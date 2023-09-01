using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.HealthCheck;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;
using Quaestor.KeyValueStore;
using Quaestor.ServiceDiscovery;
using Quaestor.Utilities;

namespace Quaestor.LoadBalancing
{
	public class LoadBalancingService : BackgroundService
	{
		private static readonly ILogger<LoadBalancingService> _logger =
			Log.CreateLogger<LoadBalancingService>();

		private readonly LoadBalancerConfig _loadBalancerConfig;

		private Server _server;

		private bool _keyValueStoreIsLocal;

		public LoadBalancingService(IConfiguration configuration)
		{
			KnownAgents.Configure(configuration);

			_loadBalancerConfig = new LoadBalancerConfig();
			configuration.GetSection(nameof(LoadBalancerConfig)).Bind(_loadBalancerConfig);

			if (string.IsNullOrEmpty(_loadBalancerConfig.HostName))
			{
				_logger.LogWarning("Invalid configuration: Hostname is null.");
			}
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await Task.CompletedTask;
		}

		public override async Task StartAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("LB service is starting...");

			// Get key-value store and service registry:
			ServiceRegistry serviceRegistry = await CreateServiceRegistryAsync();

			if (_loadBalancerConfig == null)
			{
				throw new InvalidOperationException("Missing server configuration");
			}

			_server = StartLoadBalancerService(serviceRegistry, _loadBalancerConfig);
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Stopping Service");

			if (_server != null)
			{
				GrpcServerUtils.GracefullyStop(_server);
			}

			await base.StopAsync(cancellationToken);
		}

		public override void Dispose()
		{
			_logger.LogInformation("Disposing Service");

			base.Dispose();
		}

		private async Task<ServiceRegistry> CreateServiceRegistryAsync()
		{
			Stopwatch watch = Stopwatch.StartNew();

			IKeyValueStore keyValueStore = await TryGetEtcdKeyValueStore();

			List<AgentConfiguration> serviceAgents = new List<AgentConfiguration>();

			if (keyValueStore != null)
			{
				_logger.LogInformation("Connected to distributed key-value store in {millis}ms",
					watch.ElapsedMilliseconds);
			}
			else
			{
				_logger.LogWarning(
					"Unable to connect to distributed key-value store. Using Worker agents from configuration:");

				keyValueStore = new LocalKeyValueStore();

				_keyValueStoreIsLocal = true;

				serviceAgents.AddRange(KnownAgents.Get(WellKnownAgentType.Worker));
			}

			ServiceRegistry serviceRegistry = new ServiceRegistry(keyValueStore, string.Empty);

			AddServices(serviceRegistry, serviceAgents);

			int endpointCount = serviceRegistry.GetTotalEndpointCount(out var serviceCount);

			_logger.LogInformation(
				"Service registry contains {serviceCount} service type(s) at {endpointCount} " +
				"different endpoints.", serviceCount, endpointCount);

			return serviceRegistry;
		}

		private Server StartLoadBalancerService([NotNull] ServiceRegistry serviceRegistry,
		                                        [NotNull] LoadBalancerConfig loadBalancerConfig)
		{
			ServerCredentials serverCredentials =
				GrpcServerUtils.GetServerCredentials(loadBalancerConfig.Certificate,
					loadBalancerConfig.PrivateKeyFile,
					loadBalancerConfig.EnforceMutualTls);

			var serviceDiscoveryGrpcImpl = new ServiceDiscoveryGrpcImpl(serviceRegistry)
			{
				RemoveUnhealthyServices = !_keyValueStoreIsLocal,
				WorkerResponseTimeoutSeconds = loadBalancerConfig.ServiceResponseTimeoutSeconds,
				RecentlyUsedServiceTimeoutSeconds = loadBalancerConfig.RecentlyUsedTimeoutSeconds
			};

			var health = new HealthServiceImpl();

			serviceDiscoveryGrpcImpl.Health = health;

			// General status:
			health.SetStatus(string.Empty, HealthCheckResponse.Types.ServingStatus.Serving);

			// Specifically the LB service:
			health.SetStatus(serviceDiscoveryGrpcImpl.ServiceName,
				HealthCheckResponse.Types.ServingStatus.Serving);

			_logger.LogInformation("Starting load-balancer service at {host}:{port}",
				loadBalancerConfig.HostName, loadBalancerConfig.Port);

			var server =
				new Server
				{
					Services =
					{
						ServiceDiscoveryGrpc.BindService(serviceDiscoveryGrpcImpl),
						Health.BindService(health)
					},
					Ports =
					{
						new ServerPort(loadBalancerConfig.HostName, loadBalancerConfig.Port,
							serverCredentials)
					}
				};

			server.Start();

			string protocol = serverCredentials == ServerCredentials.Insecure
				? "http"
				: "https";

			_logger.LogInformation(
				"Load balancer service is serving at {protocol}://{host}:{port}",
				protocol, loadBalancerConfig.HostName, loadBalancerConfig.Port);

			return server;
		}

		private static void AddServices(ServiceRegistry toServiceRegistry,
		                                List<AgentConfiguration> serviceAgents)
		{
			foreach (AgentConfiguration serviceAgent in serviceAgents)
			{
				_logger.LogInformation("{type}: {processCount} processes. Executable: {exe}",
					serviceAgent.AgentType, serviceAgent.ProcessCount, serviceAgent.ExecutablePath);

				List<int> ports = serviceAgent.Ports ?? new List<int>(0);

				if (ports.Count == 0)
				{
					_logger.LogWarning(
						"Agent {agentName} has no ports defined (using ephemeral ports). " +
						"Services are not added to service registry", serviceAgent.AgentType);
				}

				foreach (int port in ports)
				{
					if (serviceAgent.ServiceNames == null)
					{
						continue;
					}

					_logger.LogInformation("Adding services: {services} on {host}:{port}",
						serviceAgent.ServiceNames, serviceAgent.HostName, port);

					toServiceRegistry.Ensure(serviceAgent.ServiceNames, serviceAgent.HostName, port,
						serviceAgent.UseTls);
				}
			}
		}

		private static async Task<IKeyValueStore> TryGetEtcdKeyValueStore()
		{
			foreach (var kvsAgent in KnownAgents.Get(WellKnownAgentType.KeyValueStore))
			{
				IKeyValueStore keyValueStore = await EtcdKeyValueStore.TryConnectAsync(kvsAgent);

				if (keyValueStore != null)
				{
					return keyValueStore;
				}
			}

			// Do not just try the default address - Etcd might be running with a different purpose

			return null;
		}
	}
}
