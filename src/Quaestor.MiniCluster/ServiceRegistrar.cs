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

		/// <summary>
		///     Ensures that the services of the specified server process are registered.
		///     This method is idempotent and can be called multiple times for the same process.
		/// </summary>
		/// <param name="serverProcess"></param>
		public void Ensure(IServerProcess serverProcess)
		{
			_serviceRegistry.Ensure(serverProcess.ServiceNames, serverProcess.HostName,
				serverProcess.Port, serverProcess.UseTls);
		}

		/// <summary>
		///     Ensures that the services of the specified server process are un-registered.
		///     This method is idempotent and can be called multiple times for the same process.
		/// </summary>
		/// <param name="serverProcess"></param>
		public void EnsureRemoved(IServerProcess serverProcess)
		{
			_serviceRegistry.EnsureRemoved(serverProcess.ServiceNames, serverProcess.HostName,
				serverProcess.Port, serverProcess.UseTls);
		}
	}
}
