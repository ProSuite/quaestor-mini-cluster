using Grpc.Core;
using NUnit.Framework;
using Quaestor.KeyValueStore;
using Quaestor.Microservices.Definitions;

namespace Quaestor.Microservices.Tests
{
	[TestFixture]
	public class ServiceDiscoveryGrpcImplTest
	{
		private static string Host => "127.0.0.1";
		private static int Port => 5149;

		private ServiceRegistry _serviceRegistry;

		[OneTimeSetUp]
		public void SetupFixture()
		{
			_serviceRegistry = StartServiceDiscoveryService();
		}

		[Test]
		public void CanDiscoverServices()
		{
			ServiceDiscoveryGrpc.ServiceDiscoveryGrpcClient client = GetClient();

			// Registered service to be discovered:
			const string serviceName = "TestService";
			const string hostName = "localhost";
			const int port = 5150;

			_serviceRegistry.Add(serviceName, hostName, port);

			var response = client.LocateServices(
				new LocateServicesRequest {ServiceName = serviceName});

			Assert.AreEqual(1, response.ServiceLocations.Count);

			ServiceLocationMsg serviceLocation = response.ServiceLocations[0];
			Assert.AreEqual(serviceName, serviceLocation.ServiceName);
			Assert.AreEqual(hostName, serviceLocation.HostName);
			Assert.AreEqual(port, serviceLocation.Port);
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

			var serviceDiscoveryGrpcImpl = new ServiceDiscoveryGrpcImpl(serviceRegistry);

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
