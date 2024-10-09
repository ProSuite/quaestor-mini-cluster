using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using dotnet_etcd;
using Etcdserverpb;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;
using Quaestor.Utilities;

namespace Quaestor.KeyValueStore
{
	public class EtcdKeyValueStore : IKeyValueStore
	{
		private static readonly ILogger<EtcdKeyValueStore> _logger =
			Log.CreateLogger<EtcdKeyValueStore>();

		private readonly EtcdClient _etcdClient;
		[CanBeNull] private readonly TimeSpan? _timeOut;

		/// <summary>
		///     Attempts a connection with the Etcd grpc service at the specified
		///     address and returns the respective client endpoint as EtcdKeyValueStore.
		/// </summary>
		/// <param name="connectionString">
		///     The connection string in the form
		///     http://localhost:2379 or https://hostname.example.com:8080
		/// </param>
		/// <param name="caCert"></param>
		/// <returns></returns>
		[ItemCanBeNull]
		public static async Task<EtcdKeyValueStore> TryConnectAsync(
			string connectionString = "http://localhost:2379",
			string caCert = "")
		{
			var etcdConnectTimeout = new TimeSpan(0, 15, 0);

			_logger.LogDebug("Trying to connect to distributed key-value store at {conn} (Time-out is set to {timeout})...",
				connectionString, etcdConnectTimeout);

			int defaultPort = 2379;
			EtcdClient etcdClient = new EtcdClient(connectionString, defaultPort, caCert);

			var etcdStore = new EtcdKeyValueStore(etcdClient, etcdConnectTimeout);

			// Warm up:
			bool connected = await etcdStore.ConnectAsync();

			return connected ? etcdStore : null;
		}

		/// <summary>
		///     Attempts a connection with the Etcd grpc service at the specified
		///     address and returns the respective client endpoint as EtcdKeyValueStore.
		/// </summary>
		/// <param name="hostName"></param>
		/// <param name="port"></param>
		/// <param name="useTLS"></param>
		/// <returns></returns>
		[ItemCanBeNull]
		public static async Task<EtcdKeyValueStore> TryConnectAsync(
			string hostName, int port, bool useTLS)
		{
			string protocol = useTLS ? "https" : "http";

			string etcdConnection = $"{protocol}://{hostName}:{port}";

			string rootCertificatesAsPem =
				useTLS ? CertificateUtils.GetUserRootCertificatesInPemFormat() : string.Empty;

			var keyValueStore = await TryConnectAsync(etcdConnection, rootCertificatesAsPem);

			return keyValueStore;
		}

		/// <summary>
		///     Attempts a connection with the Etcd grpc service addresses as defined in
		///     the provided agent configuration. The first successful connection is returned
		///     as EtcdKeyValueStore.
		/// </summary>
		/// <param name="agentConfiguration"></param>
		/// <returns></returns>
		[ItemCanBeNull]
		public static async Task<EtcdKeyValueStore> TryConnectAsync(
			AgentConfiguration agentConfiguration)
		{
			List<int> ports = agentConfiguration.Ports ?? new List<int>(0);

			foreach (int port in ports)
			{
				EtcdKeyValueStore keyValueStore = await TryConnectAsync(
					agentConfiguration.HostName, port, agentConfiguration.UseTls);

				if (keyValueStore != null)
				{
					return keyValueStore;
				}
			}

			return null;
		}

		public EtcdKeyValueStore([NotNull] EtcdClient etcdClient,
		                         [CanBeNull] TimeSpan? timeOut = null)
		{
			_etcdClient = etcdClient;
			_timeOut = timeOut;
		}

		/// <summary>
		///     Optional connection warm-up to speed up initial access.
		/// </summary>
		public async Task<bool> ConnectAsync()
		{
			try
			{
				var statusRequest = new StatusRequest();

				var deadline = GetTimeOut();

				StatusResponse response =
					await _etcdClient.StatusASync(statusRequest, deadline: deadline);

				_logger.LogDebug("Successfully connected to etcd: {response}", response.ToString());
			}
			catch (Exception e)
			{
				_logger.LogWarning(e, "Error connecting to etcd using {client}", _etcdClient);
				return false;
			}

			return true;
		}

		public void Put(string key, string value)
		{
			if (key == null)
				throw new ArgumentNullException(nameof(key));

			if (string.IsNullOrEmpty(key))
				throw new ArgumentException(nameof(key));

			if (value == null)
				throw new ArgumentNullException(nameof(value));

			DateTime? deadline = GetTimeOut();

			_etcdClient.Put(key, value, null, deadline);
		}

		public string GetValue(string key)
		{
			DateTime? deadline = GetTimeOut();

			return _etcdClient.GetVal(key, null, deadline);
		}

		public IEnumerable<KeyValuePair<string, string>> GetRange(string keyPrefix)
		{
			DateTime? deadline = GetTimeOut();

			return _etcdClient.GetRangeVal(keyPrefix, null, deadline);
		}

		public void Delete(string key)
		{
			DateTime? deadline = GetTimeOut();

			try
			{
				_etcdClient.Delete(key, null, deadline);
			}
			catch (Exception e)
			{
				_logger.LogWarning(e, "Error deleting key from key-value store.");
			}
		}

		private DateTime? GetTimeOut()
		{
			if (_timeOut == null)
			{
				return null;
			}

			return DateTime.UtcNow.Add(_timeOut.Value);
		}
	}
}
