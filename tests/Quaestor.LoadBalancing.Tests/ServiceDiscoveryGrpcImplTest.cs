using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
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
		private const string _host127001 = "127.0.0.1";
		private const int _port = 5149;

		private ServiceRegistry _serviceRegistry;

		// Registered services to be discovered:
		const string _serviceName = "TestService";
		const string _localHost = "localhost";
		const int _startPort = 5151;
		const int _serviceCount = 12;
		private const int _startPort127001 = _startPort + _serviceCount;

		private readonly IDictionary<ServiceLocation, ServiceLoad> _serviceLoadByLocation =
			new Dictionary<ServiceLocation, ServiceLoad>();

		[OneTimeSetUp]
		public void SetupFixture()
		{
			_serviceRegistry = StartServiceDiscoveryService();

			StartAndRegisterServices(_localHost, _startPort);
		}

		[SetUp]
		public void TestSetup()
		{
			// Reset current load
			foreach (ServiceLoad serviceLoad in _serviceLoadByLocation.Values)
			{
				serviceLoad.CurrentProcessCount = 0;
			}
		}

		private void StartAndRegisterServices(string hostName, int startPort)
		{
			for (int port = startPort; port < startPort + _serviceCount; port++)
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
							new ServerPort(hostName, port, ServerCredentials.Insecure)
						}
					};

				server.Start();

				_serviceLoadByLocation.Add(new ServiceLocation(_serviceName, hostName, port, false),
					serviceLoad);

				_serviceRegistry.Ensure(_serviceName, hostName, port, false);
			}
		}

		private void DeregisterServices(string hostName, int startPort)
		{
			for (int port = startPort; port < startPort + _serviceCount; port++)
			{
				var serviceLocation = new ServiceLocation(_serviceName, hostName, port, false);
				_serviceLoadByLocation.Remove(serviceLocation);

				_serviceRegistry.EnsureRemoved(_serviceName, hostName, port, false);
			}
		}

		[Test]
		public void CanDiscoverSingleService()
		{
			DeregisterServices(_host127001, _startPort127001);

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
			Assert.AreEqual(_localHost, serviceLocation.HostName);
		}

		[Test]
		public void CanDiscoverManyServices()
		{
			DeregisterServices(_host127001, _startPort127001);

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
				Assert.AreEqual(_localHost, serviceLocation.HostName);
				Assert.True(serviceLocation.Port >= _startPort &&
				            serviceLocation.Port < _startPort + _serviceCount);
			}
		}

		[Test]
		public void CanBalanceLoad()
		{
			ServiceDiscoveryGrpc.ServiceDiscoveryGrpcClient client = GetClient();

			// All load at 0
			var singleServiceRequest = new DiscoverServicesRequest
			{
				ServiceName = _serviceName,
				MaxCount = 1
			};

			DiscoverServicesResponse response = client.DiscoverTopServices(singleServiceRequest);
			Assert.AreEqual(1, response.ServiceLocations.Count);

			// If all else is equal, they are ordered by port
			var serviceLocation = response.ServiceLocations[0];
			Assert.AreEqual(_serviceName, serviceLocation.ServiceName);
			Assert.AreEqual(_localHost, serviceLocation.HostName);
			Assert.AreEqual(_startPort, serviceLocation.Port);

			// The second time, thanks to some kind of round-robin among the least recently used
			// a different port should be returned:
			response = client.DiscoverTopServices(singleServiceRequest);
			Assert.AreEqual(1, response.ServiceLocations.Count);

			serviceLocation = response.ServiceLocations[0];
			Assert.AreEqual(_serviceName, serviceLocation.ServiceName);
			Assert.AreEqual(_localHost, serviceLocation.HostName);
			Assert.AreNotEqual(_startPort, serviceLocation.Port);

			foreach (KeyValuePair<ServiceLocation, ServiceLoad> loadByPort in
			         _serviceLoadByLocation)
			{
				// Order by port but descending

				int ascendingRank = loadByPort.Key.Port - _startPort;
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
				serviceLocation = response.ServiceLocations[i];

				Assert.AreEqual(_serviceName, serviceLocation.ServiceName);
				Assert.AreEqual(_localHost, serviceLocation.HostName);

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

		[Test]
		public void CanBalanceMultiMachineLoad()
		{
			// Add a 'second host' with a different name (127.0.0.1 vs localhost)
			StartAndRegisterServices(_host127001, _startPort127001);

			// Wait until the least recently used service cache can be cleared
			// (it could contain ports from previous tests)
			Thread.Sleep(5000);

			ServiceDiscoveryGrpc.ServiceDiscoveryGrpcClient client = GetClient();

			// All load at 0
			var singleServiceRequest = new DiscoverServicesRequest
			{
				ServiceName = _serviceName,
				MaxCount = 1
			};

			DiscoverServicesResponse response = client.DiscoverTopServices(singleServiceRequest);
			Assert.AreEqual(1, response.ServiceLocations.Count);

			// If all else is equal, they are ordered by port
			ServiceLocation serviceLocation = ToServiceLocation(response.ServiceLocations[0]);

			Assert.AreEqual(_serviceName, serviceLocation.ServiceName);
			string firstLocationHost = serviceLocation.HostName;

			// Add one load to the first machine:
			_serviceLoadByLocation[serviceLocation].CurrentProcessCount += 1;

			// The second time, thanks to machine-level cpu balancing the first port from the other
			// machine should be returned:
			response = client.DiscoverTopServices(singleServiceRequest);
			Assert.AreEqual(1, response.ServiceLocations.Count);

			serviceLocation = ToServiceLocation(response.ServiceLocations[0]);
			Assert.AreEqual(_serviceName, serviceLocation.ServiceName);
			Assert.AreNotEqual(firstLocationHost, serviceLocation.HostName);

			foreach (KeyValuePair<ServiceLocation, ServiceLoad> loadByPort in
			         _serviceLoadByLocation)
			{
				if (loadByPort.Key.HostName == _localHost)
				{
					continue;
				}

				// Overload the 127.0.0.1 host completely:
				int ascendingRank = loadByPort.Key.Port - _startPort127001;
				int descendingRank = _serviceCount - ascendingRank;

				loadByPort.Value.CurrentProcessCount = descendingRank;
			}

			Stopwatch watch = Stopwatch.StartNew();

			response = client.DiscoverTopServices(
				new DiscoverServicesRequest
				{
					ServiceName = _serviceName,
					MaxCount = 6
				});

			watch.Stop();
			Console.WriteLine("First time full balancing: {0}ms", watch.ElapsedMilliseconds);

			Assert.AreEqual(6, response.ServiceLocations.Count);

			HashSet<int> usedPorts = new HashSet<int>();

			for (var i = 0; i < response.ServiceLocations.Count; i++)
			{
				serviceLocation = ToServiceLocation(response.ServiceLocations[i]);

				Assert.AreEqual(_serviceName, serviceLocation.ServiceName);
				Assert.AreEqual(_localHost, serviceLocation.HostName);

				Assert.True(serviceLocation.Port >= _startPort &&
				            serviceLocation.Port < _startPort + _serviceCount);

				usedPorts.Add(serviceLocation.Port);
			}

			// Assert performance after warm-up:

			watch = Stopwatch.StartNew();

			response = client.DiscoverTopServices(
				new DiscoverServicesRequest
				{
					ServiceName = _serviceName,
					MaxCount = 4
				});

			Assert.AreEqual(4, response.ServiceLocations.Count);

			watch.Stop();

			Console.WriteLine("Second time full balancing (warm channels): {0}ms",
				watch.ElapsedMilliseconds);

			Assert.Less(watch.ElapsedMilliseconds, 50);

			for (var i = 0; i < response.ServiceLocations.Count; i++)
			{
				serviceLocation = ToServiceLocation(response.ServiceLocations[i]);

				Assert.AreEqual(_serviceName, serviceLocation.ServiceName);
				Assert.AreEqual(_localHost, serviceLocation.HostName);

				Assert.True(serviceLocation.Port >= _startPort &&
				            serviceLocation.Port < _startPort + _serviceCount);

				Assert.IsFalse(usedPorts.Contains(serviceLocation.Port));
			}

			DeregisterServices(_host127001, _startPort127001);
		}

		private static ServiceLocation ToServiceLocation(ServiceLocationMsg serviceLocationMsg)
		{
			return new ServiceLocation(serviceLocationMsg.ServiceName, serviceLocationMsg.HostName,
				serviceLocationMsg.Port, false);
		}

		private static ServiceDiscoveryGrpc.ServiceDiscoveryGrpcClient GetClient()
		{
			var client = new ServiceDiscoveryGrpc.ServiceDiscoveryGrpcClient(
				new Channel(_host127001, _port, ChannelCredentials.Insecure));

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
						new ServerPort(_host127001, _port, ServerCredentials.Insecure)
					}
				};

			server.Start();

			return serviceRegistry;
		}
	}
}
