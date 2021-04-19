using System;
using System.Collections.Generic;
using System.Diagnostics;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.HealthCheck;
using NUnit.Framework;
using Quaestor.KeyValueStore;
using Quaestor.LoadReporting;
using Quaestor.ServiceDiscovery;

namespace Quaestor.LoadBalancing.Tests
{
	[TestFixture]
	public class ServiceDiscoveryGrpcImplTest
	{
		private static string Host => "127.0.0.1";
		private static int Port => 5149;

		private ServiceRegistry _serviceRegistry;

		// Registered services to be discovered:
		const string _serviceName = "TestService";
		const string _hostName = "localhost";
		const int _startPort = 5151;
		const int _serviceCount = 12;

		private readonly IDictionary<int, ServiceLoad> _serviceLoadByPort =
			new Dictionary<int, ServiceLoad>();

		[OneTimeSetUp]
		public void SetupFixture()
		{
			_serviceRegistry = StartServiceDiscoveryService();

			for (int port = _startPort; port < _startPort + _serviceCount; port++)
			{
				var healthService = new HealthServiceImpl();

				healthService.SetStatus(_serviceName,
					HealthCheckResponse.Types.ServingStatus.Serving);

				var loadReportingService = new LoadReportingGrpcImpl();
				ServiceLoad serviceLoad = new ServiceLoad(3);

				loadReportingService.AllowMonitoring(_serviceName, serviceLoad);

				var server =
					new Server
					{
						Services =
						{
							Health.BindService(healthService),
							LoadReportingGrpc.BindService(loadReportingService)
						},
						Ports =
						{
							new ServerPort(Host, port, ServerCredentials.Insecure)
						}
					};

				server.Start();

				_serviceLoadByPort.Add(port, serviceLoad);

				_serviceRegistry.Ensure(_serviceName, _hostName, port, false);
			}
		}

		[Test]
		public void CanDiscoverSingleService()
		{
			ServiceDiscoveryGrpc.ServiceDiscoveryGrpcClient client = GetClient();

			DiscoverServicesResponse response = client.DiscoverServices(
				new DiscoverServicesRequest
				{
					ServiceName = _serviceName,
					MaxCount = 1
				});

			Assert.AreEqual(1, response.ServiceLocations.Count);

			ServiceLocationMsg serviceLocation = response.ServiceLocations[0];
			Assert.AreEqual(_serviceName, serviceLocation.ServiceName);
			Assert.AreEqual(_hostName, serviceLocation.HostName);
		}

		[Test]
		public void CanDiscoverManyServices()
		{
			ServiceDiscoveryGrpc.ServiceDiscoveryGrpcClient client = GetClient();

			DiscoverServicesResponse response = client.DiscoverServices(
				new DiscoverServicesRequest
				{
					ServiceName = _serviceName,
					MaxCount = 3
				});

			Assert.AreEqual(3, response.ServiceLocations.Count);

			foreach (var serviceLocation in response.ServiceLocations)
			{
				Assert.AreEqual(_serviceName, serviceLocation.ServiceName);
				Assert.AreEqual(_hostName, serviceLocation.HostName);
				Assert.True(serviceLocation.Port >= _startPort &&
				            serviceLocation.Port < _startPort + _serviceCount);
			}
		}

		[Test]
		public void CanBalanceLoad()
		{
			ServiceDiscoveryGrpc.ServiceDiscoveryGrpcClient client = GetClient();

			// All load at 0
			DiscoverServicesResponse response = client.DiscoverTopServices(
				new DiscoverServicesRequest
				{
					ServiceName = _serviceName,
					MaxCount = 1
				});

			Assert.AreEqual(1, response.ServiceLocations.Count);

			foreach (var serviceLocation in response.ServiceLocations)
			{
				Assert.AreEqual(_serviceName, serviceLocation.ServiceName);
				Assert.AreEqual(_hostName, serviceLocation.HostName);
				Assert.True(serviceLocation.Port >= _startPort &&
				            serviceLocation.Port < _startPort + _serviceCount);
			}

			foreach (KeyValuePair<int, ServiceLoad> loadByPort in _serviceLoadByPort)
			{
				// Order by port but descending

				int ascendingRank = loadByPort.Key - _startPort;
				int descendingRank = _serviceCount - ascendingRank;

				loadByPort.Value.CurrentProcessCount = descendingRank;
			}

			Stopwatch watch = Stopwatch.StartNew();

			response = client.DiscoverTopServices(
				new DiscoverServicesRequest
				{
					ServiceName = _serviceName,
					MaxCount = 3
				});

			watch.Stop();
			Console.WriteLine("First time full balancing: {0}ms", watch.ElapsedMilliseconds);

			Assert.AreEqual(3, response.ServiceLocations.Count);

			for (var i = 0; i < response.ServiceLocations.Count; i++)
			{
				var serviceLocation = response.ServiceLocations[i];

				Assert.AreEqual(_serviceName, serviceLocation.ServiceName);
				Assert.AreEqual(_hostName, serviceLocation.HostName);

				int expected = _startPort + _serviceCount - i - 1;
				Assert.AreEqual(expected, serviceLocation.Port);

				Assert.True(serviceLocation.Port >= _startPort &&
				            serviceLocation.Port < _startPort + _serviceCount);
			}

			// Assert performance after warm-up:

			watch = Stopwatch.StartNew();

			response = client.DiscoverTopServices(
				new DiscoverServicesRequest
				{
					ServiceName = _serviceName,
					MaxCount = 3
				});

			Assert.AreEqual(3, response.ServiceLocations.Count);

			watch.Stop();

			Console.WriteLine("Second time full balancing (warm channels): {0}ms",
				watch.ElapsedMilliseconds);
			Assert.Less(watch.ElapsedMilliseconds, 50);
		}

		private static ServiceDiscoveryGrpc.ServiceDiscoveryGrpcClient GetClient()
		{
			var client = new ServiceDiscoveryGrpc.ServiceDiscoveryGrpcClient(
				new Channel(Host, Port, ChannelCredentials.Insecure));

			return client;
		}

		private static ServiceRegistry StartServiceDiscoveryService()
		{
			var serviceRegistry = new ServiceRegistry(new LocalKeyValueStore(), "test");

			var serviceDiscoveryGrpcImpl =
				new ServiceDiscoveryGrpcImpl(serviceRegistry);

			var server =
				new Server
				{
					Services =
					{
						ServiceDiscoveryGrpc.BindService(serviceDiscoveryGrpcImpl)
					},
					Ports =
					{
						new ServerPort(Host, Port, ServerCredentials.Insecure)
					}
				};

			server.Start();

			return serviceRegistry;
		}
	}
}
