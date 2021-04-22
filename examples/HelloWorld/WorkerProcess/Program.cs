using System;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.HealthCheck;
using JetBrains.Annotations;
using Quaestor.LoadReporting;

namespace WorkerProcess
{
	[UsedImplicitly]
	internal class Program
	{
		const string _serviceName = "Worker";

		const string _exitWhenUnhealthyEnvVar = "QUAESTOR_WORKER_EXIT_WHEN_UNHEALTHY";

		private static ServiceLoad Load { get; set; }

		static async Task Main(string[] args)
		{
			if (!TryGetArguments(args, out int port, out int secondsUntilUnhealthy))
			{
				return;
			}

			// The health service every serious grpc server has:
			var healthService = new HealthServiceImpl();
			healthService.SetStatus(_serviceName, HealthCheckResponse.Types.ServingStatus.Serving);

			// The load reporting service required for Quaestor load-balancer:
			LoadReportingGrpcImpl loadReporter = new LoadReportingGrpcImpl();

			// Use Load.StartRequest(); at the beginning
			// and Load.StartRequest(); at the end of a request
			// or assign a known load rate using Load.KnownLoadRate
			Load = new ServiceLoad
			{
				ProcessCapacity = 1,
				CurrentProcessCount = 0,
				ServerUtilization = 0.12345
			};

			loadReporter.AllowMonitoring("Worker", Load);

			var server =
				new Server
				{
					Services =
					{
						// YourGrpc.BindService(yourActualServiceImpl),
						Health.BindService(healthService),
						LoadReportingGrpc.BindService(loadReporter)
					},
					Ports =
					{
						new ServerPort("localhost", port, ServerCredentials.Insecure)
					}
				};

			server.Start();

			if (secondsUntilUnhealthy > 0)
			{
				await SetUnhealthyAfter(secondsUntilUnhealthy, healthService);
			}

			Console.WriteLine("Press any key to finish.");
			Console.ReadKey(true);
		}

		private static bool TryGetArguments(string[] args,
		                                    out int port,
		                                    out int secondsUntilUnhealthy)
		{
			port = -1;
			secondsUntilUnhealthy = -1;

			if (args.Length < 1)
			{
				Console.WriteLine("Too few arguments.");
				PrintUsage();
				return false;
			}

			if (!int.TryParse(args[0], out port))
			{
				Console.WriteLine($"Invalid port: {port}.");
				PrintUsage();
				return false;
			}

			if (args.Length > 1)
			{
				if (!int.TryParse(args[1], out secondsUntilUnhealthy))

				{
					Console.WriteLine($"Invalid number of seconds: {secondsUntilUnhealthy}.");
					PrintUsage();
					return false;
				}
			}

			return true;
		}

		private static async Task SetUnhealthyAfter(int seconds, HealthServiceImpl healthService)
		{
			Console.WriteLine($"Running service in healthy mode for {seconds}s...");

			await Task.Delay(TimeSpan.FromSeconds(seconds));

			Console.WriteLine("Setting service to NOT_SERVING.");

			healthService.SetStatus(_serviceName,
				HealthCheckResponse.Types.ServingStatus.NotServing);

			bool exit = ExitWhenUnhealthy();

			if (exit)
			{
				Environment.Exit(42);
			}
		}

		private static bool ExitWhenUnhealthy()
		{
			string exitWhenUnhealthy =
				Environment.GetEnvironmentVariable(_exitWhenUnhealthyEnvVar);

			bool exit = !string.IsNullOrEmpty(exitWhenUnhealthy) &&
			            exitWhenUnhealthy.Equals("TRUE",
				            StringComparison.InvariantCultureIgnoreCase);
			return exit;
		}

		private static void PrintUsage()
		{
			Console.WriteLine("WorkerProcess <port> {seconds until unhealthy}");
		}
	}
}
