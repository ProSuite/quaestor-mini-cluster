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

			LocalProcess localProcess = GetWorkerProcess();

			cluster.Add(localProcess);

			_ = cluster.StartAsync();

			Console.WriteLine("Press any key to finish");

			Console.ReadKey();

			cluster.Abort();
		}

		private static LocalProcess GetWorkerProcess()
		{
			string exePath = GetExePath();

			int port = 5432;
			int unhealthyAfterSeconds = 75;

			var managedProcess = new LocalProcess(
				WellKnownAgentType.Worker.ToString(), exePath, $"{port} {unhealthyAfterSeconds}",
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

			string exePath = Path.Combine(assemblyDir, @"..\..\..\..",
				@"WorkerProcess\bin\Debug\net5.0",
				"WorkerProcess.exe");
			return exePath;
		}
	}
}
