using System.Collections.Generic;
using System.Threading;

namespace Quaestor.ProcessAdministration
{
	public class RequestAdmin : IRequestAdmin
	{
		private readonly List<CancelableRequest> _requests = new List<CancelableRequest>();

		#region Implementation of IRequestAdmin

		public void CancelAllRequests()
		{
			foreach (CancelableRequest request in _requests)
			{
				request.CancellationSource.Cancel();
			}
		}

		public void CancelRequest(string requestUserName, string environment)
		{
			foreach (CancelableRequest request in _requests)
			{
				if (request.RequestUserName == requestUserName &&
				    request.Environment == environment)
				{
					//_msg.WarnFormat(
					//	"Canceling request for user '{0}' in environment '{1}'",
					//	request.RequestUserName, request.Environment);
					request.CancellationSource.Cancel();
				}
			}
		}

		public CancelableRequest RegisterRequest(string requestUserName, string environment,
		                                         CancellationTokenSource cancellationSource)
		{
			var request = new CancelableRequest(requestUserName, environment, cancellationSource);

			_requests.Add(request);

			return request;
		}

		public void UnregisterRequest(CancelableRequest request)
		{
			_requests.Remove(request);
		}

		#endregion
	}
}
