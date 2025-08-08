using System;
using System.IO;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;
using Quaestor.MiniCluster;

namespace HelloWorld
{
	[UsedImplicitly]
	internal class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Hello World! Starting cluster...");

			ILoggerFactory loggerFactory =
				LoggerFactory.Create(builder => { builder.AddConsole(); });

			Log.SetLoggerFactory(loggerFactory);

			Cluster cluster = new Cluster();

			// Test process that becomes unhealthy after 75 seconds
			LocalProcess localProcessWithBadHealth = GetWorkerProcess(5432, 75);
			cluster.Add(localProcessWithBadHealth);

			// Test recycling by cluster after 3 minutes
			LocalProcess localProcessWithGoodHealth = GetWorkerProcess(5433, -1);
			localProcessWithGoodHealth.RecyclingIntervalHours = 0.05;
			cluster.Add(localProcessWithGoodHealth);

			// Test recycling for busy process after 4.5 minutes
			LocalProcess localProcessWithGoodHealthButAlwaysBusy = GetWorkerProcess(5434, -1, 1);
			localProcessWithGoodHealthButAlwaysBusy.RecyclingIntervalHours = 0.075;
			cluster.Add(localProcessWithGoodHealthButAlwaysBusy);

			_ = cluster.StartAsync();

			Console.WriteLine("Press any key to finish");

			Console.ReadKey();

			cluster.Abort();
		}

		private static LocalProcess GetWorkerProcess(int port, int unhealthyAfterSeconds,
		                                             int currentRequests = 0)
		{
			string exePath = GetExePath();

			var managedProcess = new LocalProcess(
				WellKnownAgentType.Worker.ToString(), exePath,
				$"{port} {unhealthyAfterSeconds} {currentRequests}",
				"localhost", port);

			managedProcess.ServiceNames.Add("Worker");

			managedProcess.PrioritizeAvailability = true;

			return managedProcess;
		}

		private static string GetExePath()
		{
			string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

			if (assemblyDir == null)
			{
				throw new InvalidOperationException("Cannot get directory of executing assembly.");
			}

			const string targetFramework = "net8.0";
			const string buildConfiguration = "Debug";

			string exePath = Path.Combine(assemblyDir, @"..\..\..\..",
				@$"WorkerProcess\bin\{buildConfiguration}\{targetFramework}",
				"WorkerProcess.exe");
			return exePath;
		}
	}
}
