using System;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.HealthCheck;
using JetBrains.Annotations;

namespace WorkerProcess
{
	[UsedImplicitly]
	internal class Program
	{
		const string _serviceName = "Worker";

		static async Task Main(string[] args)
		{
			int port;

			if (args.Length < 1)
			{
				Console.WriteLine("Too few arguments.");
				PrintUsage();
				return;
			}

			if (!int.TryParse(args[0], out port))
			{
				Console.WriteLine($"Invalid port: {port}.");
				PrintUsage();
				return;
			}

			int seconds = -1;
			if (args.Length > 1)
			{
				if (!int.TryParse(args[1], out seconds))

				{
					Console.WriteLine($"Invalid number of seconds: {seconds}.");
					PrintUsage();
					return;
				}
			}

			var healthService = new HealthServiceImpl();

			healthService.SetStatus(_serviceName, HealthCheckResponse.Types.ServingStatus.Serving);

			var server =
				new Server
				{
					Services =
					{
						Health.BindService(healthService)
					},
					Ports =
					{
						new ServerPort("localhost", port, ServerCredentials.Insecure)
					}
				};

			server.Start();

			if (seconds > 0)
			{
				Console.WriteLine($"Running service in healthy mode for {seconds}s...");

				await Task.Delay(TimeSpan.FromSeconds(seconds));

				Console.WriteLine("Setting service to NOT_SERVING.");

				healthService.SetStatus(_serviceName, HealthCheckResponse.Types.ServingStatus.NotServing);
			}

			Console.WriteLine("Press any key to finish.");

			Console.ReadKey();
		}

		private static void PrintUsage()
		{
			Console.WriteLine("WorkerProcess <port> {seconds until unhealthy}");
		}
	}
}
