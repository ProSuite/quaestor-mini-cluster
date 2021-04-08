using JetBrains.Annotations;
using Quaestor.KeyValueStore;

namespace Quaestor.MiniCluster
{
	public class ServiceRegistrar
	{
		[NotNull] private readonly ServiceRegistry _serviceRegistry;

		public ServiceRegistrar([NotNull] ServiceRegistry serviceRegistry)
		{
			_serviceRegistry = serviceRegistry;
		}

		public void Add(IServerProcess serverProcess)
		{
			_serviceRegistry.Add(serverProcess.ServiceNames, serverProcess.HostName,
				serverProcess.Port);
		}

		public void EnsureRemoved(IServerProcess serverProcess)
		{
			_serviceRegistry.EnsureRemoved(serverProcess.ServiceNames, serverProcess.HostName,
				serverProcess.Port);
		}
	}
}
