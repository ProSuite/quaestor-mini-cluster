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
		private static readonly string _logFilePath = Path.Combine(Path.GetTempPath(), "RequestAdmin.log");


		#region Implementation of IRequestAdmin

		public void CancelAllRequests()
		{
			Log($"[CancelAllRequests] Cancelling {_requests.Count} requests");

			foreach (CancelableRequest request in _requests)
			{
				Log($"Cancelling: User='{request.RequestUserName}', Environment='{request.Environment}'");
				request.CancellationSource.Cancel();
			}
		}

		public void CancelRequest(string requestUserName, string environment)
		{
			Log("[CancelRequest] METHOD ENTERED");
			Log($"[CancelRequest] Requested for: User='{requestUserName}', Env='{environment}'");

			Log("Currently registered requests:");
			foreach (CancelableRequest r in _requests)
			{
				Log($" → Registered: User='{r.RequestUserName}', Env='{r.Environment}'");
			}

			Log("[CancelRequest] Searching _requests list for matching user/environment...");

			foreach (CancelableRequest request in _requests)
			{
				Log($"Checking: User='{request.RequestUserName}', Env='{request.Environment}'");

				if (request.RequestUserName == requestUserName &&
					request.Environment == environment)
				{
					Log(" Match found → Cancelling now...");
					request.CancellationSource.Cancel();
				}
			}
		}



		public CancelableRequest RegisterRequest(string requestUserName, string environment,
												 CancellationTokenSource cancellationSource)
		{
			var request = new CancelableRequest(requestUserName, environment, cancellationSource);

			Log($"[RegisterRequest] Registered: User='{requestUserName}', Env='{environment}'");

			_requests.Add(request);

			// Log current list of registered requests
			Log("[RegisterRequest] Current state of _requests list:");
			foreach (CancelableRequest r in _requests)
			{
				Log($" → User='{r.RequestUserName}', Env='{r.Environment}'");
			}

			return request;
		}


		public void UnregisterRequest(CancelableRequest request)
		{
			Log($"[UnregisterRequest] Unregistered: User='{request.RequestUserName}', Env='{request.Environment}'");

			_requests.Remove(request);
		}

		#endregion

		#region Logging Helper

		private static void Log(string message)
		{
			var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";

			Debug.WriteLine(timestamped);

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
