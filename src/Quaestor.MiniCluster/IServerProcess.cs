using System.Collections.Generic;

namespace Quaestor.MiniCluster
{
	public interface IServerProcess
	{
		string HostName { get; }

		int Port { get; }

		IList<string> ServiceNames { get; }
	}
}
