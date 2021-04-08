using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using dotnet_etcd;
using Etcdserverpb;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;

namespace Quaestor.KeyValueStore
{
	public class EtcdKeyValueStore : IKeyValueStore
	{
		private readonly ILogger<EtcdKeyValueStore> _logger = Log.CreateLogger<EtcdKeyValueStore>();

		private readonly EtcdClient _etcdClient;
		[CanBeNull] private readonly TimeSpan? _timeOut;

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

				StatusResponse response = await _etcdClient.StatusASync(statusRequest);

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

			_etcdClient.Delete(key, null, deadline);
		}

		private DateTime? GetTimeOut()
		{
			if (_timeOut == null)
			{
				return null;
			}

			return DateTime.Now.Add(_timeOut.Value);
		}
	}
}
