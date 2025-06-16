using System.Threading;

namespace Quaestor.ProcessAdministration
{
	public interface IRequestAdmin
	{
		void CancelAllRequests();

		void CancelRequest(string requestUserName,
		                   string environment);

		CancelableRequest RegisterRequest(string requestUserName,
		                                  string environment,
		                                  CancellationTokenSource cancellationSource);

		void UnregisterRequest(CancelableRequest request);
	}
}
