using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Quaestor.KeyValueStore
{
	public class ServiceRegistry
	{
		private readonly IKeyValueStore _keyValueStore;
		private readonly string _globalPrefix;

		public ServiceRegistry([NotNull] IKeyValueStore keyValueStore,
		                       [NotNull] string globalPrefix)
		{
			_keyValueStore = keyValueStore;
			_globalPrefix = globalPrefix;
		}

		public void Add(IEnumerable<string> serviceNames, string hostName, int port)
		{
			foreach (string serviceName in serviceNames)
			{
				Add(serviceName, hostName, port);
			}
		}

		public void Add(string serviceName, string hostName, int port)
		{
			string key = CreateKey(serviceName, hostName, port);

			_keyValueStore.Put(key, serviceName);
		}

		public IEnumerable<ServiceLocation> GetServiceLocations(string serviceName,
		                                                        string globalScope = null)
		{
			if (globalScope == null)
			{
				globalScope = _globalPrefix;
			}

			string prefix = $"{globalScope}/services/{serviceName}/";

			foreach (var kvp in _keyValueStore.GetRange(prefix))
			{
				ServiceLocation serviceLocation = Parse(kvp.Key);

				yield return serviceLocation;
			}
		}

		private ServiceLocation Parse(string key)
		{
			string[] components = key.Split('/');

			string serviceName = components[2];
			string hostName = components[3];
			var portStr = components[4];
			if (!int.TryParse(portStr, out var port))
			{
				throw new InvalidOperationException($"Cannot parse {portStr} to int");
			}

			ServiceLocation serviceLocation = new ServiceLocation(
				serviceName, hostName, port, _globalPrefix);

			return serviceLocation;
		}

		public void EnsureRemoved(IEnumerable<string> serviceNames, string hostName, int port)
		{
			foreach (string serviceName in serviceNames)
			{
				EnsureRemoved(serviceName, hostName, port);
			}
		}

		public void EnsureRemoved(string serviceName, string hostName, int port)
		{
			string key = CreateKey(serviceName, hostName, port);

			_keyValueStore.Delete(key);
		}

		private string CreateKey(string serviceName, string hostName, int port)
		{
			return $"{_globalPrefix}/services/{serviceName}/{hostName}/{port}";
		}
	}
}
