using System;
using System.Diagnostics;
using System.Threading;
using Grpc.Core;
using NUnit.Framework;
using Quaestor.LoadReporting;
using Quaestor.Microservices.Definitions;

namespace Quaestor.Microservices.Tests
{
	[TestFixture]
	public class LoadReportingGrpcImplTest
	{
		private static string ServiceName => "TestService";

		private static string Host => "127.0.0.1";
		private static int Port => 5150;

		private ServiceLoad _actualLoad;

		[OneTimeSetUp]
		public void SetupFixture()
		{
			_actualLoad = StartLoadReportingService();
		}

		[Test]
		public void CanReportBasicLoad()
		{
			LoadReportingGrpc.LoadReportingGrpcClient client = GetClient();

			AssertCorrectReport(client, _actualLoad);

			_actualLoad.ProcessCapacity = 5;
			_actualLoad.CurrentProcessCount = 3;
			_actualLoad.CpuUsage = 12.345;

			_actualLoad.ClientCallsStarted = 13;
			_actualLoad.ClientCallsFinished = 11;

			// Note: The actualLoad's client stats are reset on report (same instance in unit test!)
			var expectedLoad = _actualLoad.Clone();

			AssertCorrectReport(client, expectedLoad);
		}

		[Test]
		public void CanReportCpuLoad()
		{
			_actualLoad.CpuUsage = _actualLoad.GetCpuUsage();

			LoadReportingGrpc.LoadReportingGrpcClient client = GetClient();

			AssertCorrectReport(client, _actualLoad);

			// Mix between 8% and 0% -> ca. 5%
			for (long i = 0; i < 10000000000; i++)
			{
				double stress = (double) i / (i + 123456789);
			}

			Thread.Sleep(10000);

			_actualLoad.CpuUsage = _actualLoad.GetCpuUsage();

			Console.WriteLine("{0} CPU: {1:0.0}%", Process.GetCurrentProcess().ProcessName,
				_actualLoad.CpuUsage * 100);

			AssertCorrectReport(client, _actualLoad);
		}

		[Test]
		public void CanGetExceptionForNonExistingService()
		{
			_actualLoad.CpuUsage = _actualLoad.GetCpuUsage();

			LoadReportingGrpc.LoadReportingGrpcClient client = GetClient();

			Assert.Throws<RpcException>(
				() => client.ReportLoad(new LoadReportRequest {ServiceName = "Does not exist"}));
		}

		private static void AssertCorrectReport(LoadReportingGrpc.LoadReportingGrpcClient client,
		                                        ServiceLoad actualLoad)
		{
			var loadResponse =
				client.ReportLoad(new LoadReportRequest {ServiceName = ServiceName});

			Assert.NotNull(loadResponse.ServerStats);
			Assert.NotNull(loadResponse.ClientStats);

			ServerStats serverStats = loadResponse.ServerStats;
			ClientStats clientStats = loadResponse.ClientStats;

			Assert.AreEqual(actualLoad.ProcessCapacity, serverStats.RequestCapacity);
			Assert.AreEqual(actualLoad.CurrentProcessCount, serverStats.CurrentRequests);
			Assert.AreEqual(actualLoad.CpuUsage, serverStats.CpuUsage);

			Assert.AreEqual(actualLoad.ClientCallsStarted, clientStats.NumCallsStarted);
			Assert.AreEqual(actualLoad.ClientCallsFinished, clientStats.NumCallsFinished);
		}

		private static LoadReportingGrpc.LoadReportingGrpcClient GetClient()
		{
			var client = new LoadReportingGrpc.LoadReportingGrpcClient(
				new Channel(Host, Port, ChannelCredentials.Insecure));

			return client;
		}

		private static ServiceLoad StartLoadReportingService()
		{
			var loadReportingServiceImpl = new LoadReportingGrpcImpl();

			var server =
				new Server
				{
					Services =
					{
						LoadReportingGrpc.BindService(loadReportingServiceImpl)
					},
					Ports =
					{
						new ServerPort(Host, Port, ServerCredentials.Insecure)
					}
				};

			ServiceLoad serviceLoad = new ServiceLoad();
			loadReportingServiceImpl.AllowMonitoring(ServiceName, serviceLoad);

			server.Start();

			return serviceLoad;
		}
	}
}
