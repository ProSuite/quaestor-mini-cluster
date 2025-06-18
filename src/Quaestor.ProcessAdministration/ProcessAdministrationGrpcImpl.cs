using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Grpc.Core;

namespace Quaestor.ProcessAdministration
{
	public class ProcessAdministrationGrpcImpl
		: ProcessAdministrationGrpc.ProcessAdministrationGrpcBase
	{
		public ProcessAdministrationGrpcImpl()
		{
			RequestAdmin = new RequestAdmin();
			Log("ProcessAdministrationGrpcImpl wurde initialisiert!");

		}

		public IRequestAdmin RequestAdmin { get; }

		#region Overrides of ProcessAdministrationGrpcBase

		public override Task<CancelResponse> Cancel(CancelRequest request,
													ServerCallContext context)
		{
			Log($"[gRPC-Cancel] ENTER Cancel(): User='{request.UserName}', Env='{request.Environment}', Peer={context.Peer}");

			if (RequestAdmin == null)
			{
				throw new InvalidOperationException("Request admin has not been initialized.");
			}

			RequestAdmin.CancelRequest(request.UserName, request.Environment);

			return Task.FromResult(new CancelResponse
			{
				Success = true
			});
		}

		#endregion

		#region Logging Helper

		private static readonly string _logFilePath = Path.Combine(Path.GetTempPath(), "ProcessAdministrationGrpc.log");

		private static void Log(string message)
		{
			var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";

			// Ausgabe in Debug- und Konsole
			Debug.WriteLine(timestamped);
			Console.WriteLine(timestamped);

			// zusätzlich in eine Datei
			try
			{
				File.AppendAllText(_logFilePath, timestamped + Environment.NewLine);
			}
			catch
			{
				// still silent if file is locked
			}
		}

		#endregion
	}
}
