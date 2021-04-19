using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Quaestor.KeyValueStore
{
	public class ServiceRegistry
	{
		private readonly IKeyValueStore _keyValueStore;
		private readonly string _globalPrefix;

		public ServiceRegistry([NotNull] IKeyValueStore keyValueStore,
		                       [NotNull] string globalPrefix = "")
		{
			_keyValueStore = keyValueStore;
			_globalPrefix = globalPrefix;
		}

		public void Ensure(IEnumerable<string> serviceNames, string hostName, int port, bool useTls)
		{
			foreach (string serviceName in serviceNames)
			{
				Ensure(serviceName, hostName, port, useTls);
			}
		}

		public void Ensure(string serviceName, string hostName, int port, bool useTls)
		{
			string key = CreateKey(serviceName, hostName, port, useTls);

			_keyValueStore.Put(key, serviceName);
		}

		public IEnumerable<ServiceLocation> GetServiceLocations(string serviceName,
		                                                        string globalScope = null)
		{
			if (globalScope == null)
			{
				globalScope = _globalPrefix;
			}

			IEnumerable<KeyValuePair<string, string>> keyValuePairs =
				GetServiceEntries(globalScope, serviceName);

			foreach (var kvp in keyValuePairs)
			{
				ServiceLocation serviceLocation = Parse(kvp.Key);

				yield return serviceLocation;
			}
		}

		public int GetTotalEndpointCount(out int distinctServiceCount)
		{
			IEnumerable<KeyValuePair<string, string>> keyValuePairs = GetServiceEntries();

			var all = new List<ServiceLocation>();

			foreach (var kvp in keyValuePairs)
			{
				ServiceLocation serviceLocation = Parse(kvp.Key);

				all.Add(serviceLocation);
			}

			distinctServiceCount = all.Select(s => s.ServiceName).Distinct().Count();
			int endPointCount = all.Select(s => $"{s.HostName}:{s.Port}").Distinct().Count();

			return endPointCount;
		}

		private IEnumerable<KeyValuePair<string, string>> GetServiceEntries(string scope = null,
			string serviceName = null)
		{
			scope = scope ?? _globalPrefix;
			string prefix = $"services/{scope}";

			if (serviceName != null)
			{
				prefix = $"{prefix}/{serviceName}";
			}

			return _keyValueStore.GetRange(prefix);
		}

		private ServiceLocation Parse(string key)
		{
			string[] components = key.Split('/');

			string serviceName = components[2];
			string hostName = components[3];
			string portStr = components[4];
			if (!int.TryParse(portStr, out var port))
			{
				throw new InvalidOperationException($"Cannot parse {portStr} to int");
			}

			string protocol = components[5];

			bool useTls = protocol == "https";

			ServiceLocation serviceLocation = new ServiceLocation(
				serviceName, hostName, port, useTls, _globalPrefix);

			return serviceLocation;
		}

		public void EnsureRemoved(IEnumerable<string> serviceNames,
		                          string hostName, int port, bool useTls)
		{
			foreach (string serviceName in serviceNames)
			{
				EnsureRemoved(serviceName, hostName, port, useTls);
			}
		}

		public void EnsureRemoved(string serviceName, string hostName, int port, bool useTls)
		{
			string key = CreateKey(serviceName, hostName, port, useTls);

			_keyValueStore.Delete(key);
		}

		private string CreateKey(string serviceName, string hostName, int port, bool useTls)
		{
			string protocol = useTls ? "https" : "http";

			return $"services/{_globalPrefix}/{serviceName}/{hostName}/{port}/{protocol}";
		}
	}
}
