using System.Threading;
using Grpc.Core;
using NUnit.Framework;

namespace Quaestor.ProcessAdministration.Tests
{
	[TestFixture]
	public class ProcessAdministrationGrpcImplTest
	{
		private static string ServiceName => "TestService";

		private static string Host => "127.0.0.1";
		private static int Port => 5150;

		private IRequestAdmin _requestAdmin;

		[OneTimeSetUp]
		public void SetupFixture()
		{
			_requestAdmin = StartProcessAdministrationService();
		}

		[Test]
		public void CannotCancelWithoutRegisteredRequest()
		{
			ProcessAdministrationGrpc.ProcessAdministrationGrpcClient client = GetClient();

			CancelResponse cancelResponse = client.Cancel(new CancelRequest()
			{
				Environment = "Test",
				ServiceName = ServiceName,
				UserName = "TestUser",
			});

			Assert.IsFalse(cancelResponse.Success);
		}

		[Test]
		public void CanCancelWithRegisteredRequest()
		{
			ProcessAdministrationGrpc.ProcessAdministrationGrpcClient client = GetClient();

			CancelableRequest cancelableRequest =
				_requestAdmin.RegisterRequest("TestUser", "Test", new CancellationTokenSource());

			CancelResponse cancelResponse = client.Cancel(new CancelRequest()
			{
				Environment = "Test",
				ServiceName = ServiceName,
				UserName = "TestUser",
			});

			Assert.IsTrue(cancelResponse.Success);

			_requestAdmin.UnregisterRequest(cancelableRequest);
		}

		//[Test]
		//public void CanReportBasicLoad()
		//{
		//	ProcessAdministrationGrpc.ProcessAdministrationGrpcClient client = GetClient();

		//	AssertCorrectReport(client, _requestAdmin);

		//	_requestAdmin.ProcessCapacity = 5;
		//	_requestAdmin.ResetCurrentProcessCount(3);
		//	_requestAdmin.ServerUtilization = 12.345;

		//	// Note: The actualLoad's start time is reset on report (same instance in unit test!)
		//	var expectedLoad = _requestAdmin.Clone();

		//	AssertCorrectReport(client, expectedLoad);
		//}

		//[Test]
		//public void CanReportKnownLoadRate()
		//{
		//	LoadReportingGrpc.LoadReportingGrpcClient client = GetClient();

		//	AssertCorrectReport(client, _requestAdmin);

		//	_requestAdmin.KnownLoadRate = 0.987654;

		//	var expectedLoad = _requestAdmin.Clone();

		//	AssertCorrectReport(client, expectedLoad);
		//}

		//[Test]
		//public void CanReportCpuLoad()
		//{
		//	_requestAdmin.ServerUtilization = _requestAdmin.GetProcessCpuUsage();

		//	LoadReportingGrpc.LoadReportingGrpcClient client = GetClient();

		//	AssertCorrectReport(client, _requestAdmin);

		//	// Mix between 8% and 0% -> ca. 5%
		//	for (long i = 0; i < 10000000000; i++)
		//	{
		//		double stress = (double)i / (i + 123456789);
		//	}

		//	Thread.Sleep(10000);

		//	_requestAdmin.ServerUtilization = _requestAdmin.GetProcessCpuUsage();

		//	Console.WriteLine("{0} CPU: {1:0.0}%", Process.GetCurrentProcess().ProcessName,
		//		_requestAdmin.ServerUtilization * 100);

		//	AssertCorrectReport(client, _requestAdmin);
		//}

		//[Test]
		//public void CanReportMemoryLoad()
		//{
		//	LoadReportingGrpc.LoadReportingGrpcClient client = GetClient();

		//	AssertCorrectReport(client, _requestAdmin);

		//	_requestAdmin.StartRequest(42.42);

		//	AssertCorrectReport(client, _requestAdmin);

		//	Thread.Sleep(5000);

		//	// Example for actual host memory usage:
		//	GCMemoryInfo gcMemoryInfo = GC.GetGCMemoryInfo();

		//	if (gcMemoryInfo.MemoryLoadBytes == 0)
		//	{
		//		GC.Collect();
		//		gcMemoryInfo = GC.GetGCMemoryInfo();
		//	}

		//	double memoryUsagePct =
		//		(double)gcMemoryInfo.MemoryLoadBytes /
		//		gcMemoryInfo.TotalAvailableMemoryBytes * 100;

		//	_requestAdmin.EndRequest(memoryUsagePct);

		//	Console.WriteLine("Host Memory: {0:0.0}%", memoryUsagePct);

		//	AssertCorrectReport(client, _requestAdmin);
		//}

		//[Test]
		//public void CanGetExceptionForNonExistingService()
		//{
		//	_requestAdmin.ServerUtilization = _requestAdmin.GetProcessCpuUsage();

		//	LoadReportingGrpc.LoadReportingGrpcClient client = GetClient();

		//	Assert.Throws<RpcException>(
		//		() => client.ReportLoad(new LoadReportRequest { ServiceName = "Does not exist" }));
		//}

		//private static void AssertCorrectReport(LoadReportingGrpc.LoadReportingGrpcClient client,
		//                                        ServiceLoad actualLoad)
		//{
		//	var loadResponse =
		//		client.ReportLoad(new LoadReportRequest { ServiceName = ServiceName });

		//	Assert.NotNull(loadResponse.ServerStats);

		//	ServerStats serverStats = loadResponse.ServerStats;

		//	Assert.AreEqual(actualLoad.ProcessCapacity, serverStats.RequestCapacity);
		//	Assert.AreEqual(actualLoad.CurrentProcessCount, serverStats.CurrentRequests);
		//	Assert.AreEqual(actualLoad.ServerUtilization, serverStats.ServerUtilization);
		//	Assert.AreEqual(actualLoad.ServerMemoryUsagePercent,
		//		serverStats.ServerMemoryUsagePercent);

		//	DateTime? reportedMemoryTime = loadResponse.TimestampTicks > 0
		//		? new DateTime(loadResponse.TimestampTicks)
		//		: null;

		//	Assert.AreEqual(actualLoad.LastUpdateTime, reportedMemoryTime);
		//}

		private static ProcessAdministrationGrpc.ProcessAdministrationGrpcClient GetClient()
		{
			var client = new ProcessAdministrationGrpc.ProcessAdministrationGrpcClient(
				new Channel(Host, Port, ChannelCredentials.Insecure));

			return client;
		}

		private static IRequestAdmin StartProcessAdministrationService()
		{
			var procAdminServiceImpl = new ProcessAdministrationGrpcImpl();

			var server =
				new Server
				{
					Services =
					{
						ProcessAdministrationGrpc.BindService(procAdminServiceImpl)
					},
					Ports =
					{
						new ServerPort(Host, Port, ServerCredentials.Insecure)
					}
				};

			server.Start();

			return procAdminServiceImpl.RequestAdmin;
		}
	}
}
