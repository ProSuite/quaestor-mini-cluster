using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;

namespace Quaestor.KeyValueStore
{
	public class LocalKeyValueStore : IKeyValueStore
	{
		private readonly ILogger _logger = Log.CreateLogger<LocalKeyValueStore>();

		private readonly ConcurrentDictionary<string, string> _dictionary =
			new ConcurrentDictionary<string, string>();

		public void Put(string key, string value)
		{
			if (key == null)
				throw new ArgumentNullException(nameof(key));

			if (string.IsNullOrEmpty(key))
				throw new ArgumentException(nameof(key));

			if (value == null)
				throw new ArgumentNullException(nameof(value));

			// TODO: Re-try if concurrent access is really used
			if (_dictionary.TryGetValue(key, out string originalValue))
			{
				bool updated = _dictionary.TryUpdate(key, value, originalValue);
				_logger.LogDebug(
					updated ? "Updated {key} in kv store" : "Could not update {key} in kv store",
					key);
			}

			bool added = _dictionary.TryAdd(key, value);

			_logger.LogDebug(added ? "Added {key} to kv store" : "Could not add {key} to kv store",
				key);
		}

		public string GetValue(string key)
		{
			_dictionary.TryGetValue(key, out var result);

			return result ?? string.Empty;
		}

		public IEnumerable<KeyValuePair<string, string>> GetRange(string keyPrefix)
		{
			return _dictionary.Where(keyValuePair => keyValuePair.Key.StartsWith(keyPrefix));
		}

		public void Delete(string key)
		{
			bool deleted = _dictionary.TryRemove(key, out _);

			_logger.LogDebug(
				deleted ? "Deleted {key} from kv store" : "Key {key} could not be deleted", key);
		}
	}
}
