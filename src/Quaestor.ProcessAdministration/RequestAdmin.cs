using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Quaestor.ProcessAdministration
{
	public class RequestAdmin : IRequestAdmin
	{
		private readonly List<CancelableRequest> _requests = new List<CancelableRequest>();

		private static readonly string _logFilePath =
			Path.Combine(Path.GetTempPath(), "RequestAdmin.log");

		private static readonly bool _logToFile =
			Environment.GetEnvironmentVariable("QUAESTOR_LOG_TO_FILE")?.ToUpper() == "TRUE";

		public RequestAdmin()
		{
			Log("ProcessAdministrationGrpcImpl initialized.");
		}

		#region Implementation of IRequestAdmin

		public bool CancelAllRequests()
		{
			Log($"[CancelAllRequests] Cancelling {_requests.Count} requests");

			bool result = false;
			foreach (CancelableRequest request in _requests)
			{
				Log($"Cancelling: User='{request.RequestUserName}', " +
				    $"Environment='{request.Environment}'");

				request.CancellationSource.Cancel();
				result = true;
			}

			return result;
		}

		public bool CancelRequest(string requestUserName, string environment)
		{
			LogCancelRequest(requestUserName, environment);

			bool result = false;
			foreach (CancelableRequest request in _requests)
			{
				if (request.RequestUserName == requestUserName &&
				    request.Environment == environment)
				{
					Log(" Match found → Cancelling now...");
					request.CancellationSource.Cancel();

					result = true;
				}
			}

			return result;
		}

		public CancelableRequest RegisterRequest(string requestUserName, string environment,
		                                         CancellationTokenSource cancellationSource)
		{
			var request = new CancelableRequest(requestUserName, environment, cancellationSource);

			Log($"[RegisterRequest] Adding: User='{requestUserName}', Env='{environment}'");

			_requests.Add(request);

			LogCurrentRequests();

			return request;
		}

		public void UnregisterRequest(CancelableRequest request)
		{
			Log(
				$"[UnregisterRequest] Unregistered: User='{request.RequestUserName}', Env='{request.Environment}'");

			_requests.Remove(request);
		}

		#endregion

		#region Logging Helper method

		private void LogCancelRequest(string requestUserName, string environment)
		{
			if (!_logToFile)
			{
				return;
			}

			Log($"[CancelRequest] Requested for: User='{requestUserName}', Env='{environment}'");

			LogCurrentRequests();

			Log("[CancelRequest] Searching _requests list for matching user/environment...");
		}

		private void LogCurrentRequests()
		{
			if (!_logToFile)
			{
				return;
			}

			Log("Currently registered requests:");
			foreach (CancelableRequest r in _requests)
			{
				Log($" → Registered: User='{r.RequestUserName}', Env='{r.Environment}'");
			}
		}

		public static void Log(string message)
		{
			var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";

			Debug.WriteLine(timestamped);

			if (!_logToFile)
			{
				// Skip file logging if not enabled
				return;
			}

			try
			{
				File.AppendAllText(_logFilePath, timestamped + Environment.NewLine);
			}
			catch
			{
				// Fail silently if file is locked or unwritable
			}
		}

		#endregion
	}
}
