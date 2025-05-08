using System;
using System.Diagnostics;
using System.Threading;
using Grpc.Core;
using NUnit.Framework;

namespace Quaestor.LoadReporting.Tests
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
			_actualLoad.ResetCurrentProcessCount(3);
			_actualLoad.ServerUtilization = 12.345;

			// Note: The actualLoad's start time is reset on report (same instance in unit test!)
			var expectedLoad = _actualLoad.Clone();

			AssertCorrectReport(client, expectedLoad);
		}

		[Test]
		public void CanReportKnownLoadRate()
		{
			LoadReportingGrpc.LoadReportingGrpcClient client = GetClient();

			AssertCorrectReport(client, _actualLoad);

			_actualLoad.KnownLoadRate = 0.987654;

			var expectedLoad = _actualLoad.Clone();

			AssertCorrectReport(client, expectedLoad);
		}

		[Test]
		public void CanReportCpuLoad()
		{
			_actualLoad.ServerUtilization = _actualLoad.GetProcessCpuUsage();

			LoadReportingGrpc.LoadReportingGrpcClient client = GetClient();

			AssertCorrectReport(client, _actualLoad);

			// Mix between 8% and 0% -> ca. 5%
			for (long i = 0; i < 10000000000; i++)
			{
				double stress = (double)i / (i + 123456789);
			}

			Thread.Sleep(10000);

			_actualLoad.ServerUtilization = _actualLoad.GetProcessCpuUsage();

			Console.WriteLine("{0} CPU: {1:0.0}%", Process.GetCurrentProcess().ProcessName,
				_actualLoad.ServerUtilization * 100);

			AssertCorrectReport(client, _actualLoad);
		}

		[Test]
		public void CanReportMemoryLoad()
		{
			LoadReportingGrpc.LoadReportingGrpcClient client = GetClient();

			AssertCorrectReport(client, _actualLoad);

			_actualLoad.StartRequest(42.42);

			AssertCorrectReport(client, _actualLoad);

			Thread.Sleep(5000);

			// Example for actual host memory usage:
			GCMemoryInfo gcMemoryInfo = GC.GetGCMemoryInfo();

			if (gcMemoryInfo.MemoryLoadBytes == 0)
			{
				GC.Collect();
				gcMemoryInfo = GC.GetGCMemoryInfo();
			}

			double memoryUsagePct =
				(double)gcMemoryInfo.MemoryLoadBytes /
				gcMemoryInfo.TotalAvailableMemoryBytes * 100;

			_actualLoad.EndRequest(memoryUsagePct);

			Console.WriteLine("Host Memory: {0:0.0}%", memoryUsagePct);

			AssertCorrectReport(client, _actualLoad);
		}

		[Test]
		public void CanGetExceptionForNonExistingService()
		{
			_actualLoad.ServerUtilization = _actualLoad.GetProcessCpuUsage();

			LoadReportingGrpc.LoadReportingGrpcClient client = GetClient();

			Assert.Throws<RpcException>(
				() => client.ReportLoad(new LoadReportRequest { ServiceName = "Does not exist" }));
		}

		private static void AssertCorrectReport(LoadReportingGrpc.LoadReportingGrpcClient client,
		                                        ServiceLoad actualLoad)
		{
			var loadResponse =
				client.ReportLoad(new LoadReportRequest { ServiceName = ServiceName });

			Assert.NotNull(loadResponse.ServerStats);

			ServerStats serverStats = loadResponse.ServerStats;

			Assert.AreEqual(actualLoad.ProcessCapacity, serverStats.RequestCapacity);
			Assert.AreEqual(actualLoad.CurrentProcessCount, serverStats.CurrentRequests);
			Assert.AreEqual(actualLoad.ServerUtilization, serverStats.ServerUtilization);
			Assert.AreEqual(actualLoad.ServerMemoryUsagePercent,
				serverStats.ServerMemoryUsagePercent);

			DateTime? reportedMemoryTime = loadResponse.TimestampTicks > 0
				? new DateTime(loadResponse.TimestampTicks)
				: null;

			Assert.AreEqual(actualLoad.LastUpdateTime, reportedMemoryTime);
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
