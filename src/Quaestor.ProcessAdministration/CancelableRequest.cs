using System.Threading;

namespace Quaestor.ProcessAdministration
{
	public class CancelableRequest
	{
		public CancelableRequest(string requestUserName,
		                         string environment,
		                         CancellationTokenSource cancellationSource)
		{
			RequestUserName = requestUserName;
			Environment = environment;
			CancellationSource = cancellationSource;
		}

		public string RequestUserName { get; }
		public string Environment { get; }
		public CancellationTokenSource CancellationSource { get; }
	}
}
