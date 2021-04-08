using System.Collections.Generic;
using JetBrains.Annotations;

namespace Quaestor.KeyValueStore
{
	public interface IKeyValueStore
	{
		/// <summary>
		///     Puts a string value into the key-value store.
		/// </summary>
		/// <param name="key">Non-null and non-empty key</param>
		/// <param name="value">Non-null but possibly empty value</param>
		void Put([NotNull] string key, [NotNull] string value);

		/// <summary>
		///     Retrieves a string value from the key-value store.
		/// </summary>
		/// <param name="key">Non-null and non-empty key</param>
		/// <returns>
		///     Non-null but possibly empty value. An empty string is
		///     also returned if the key does not exist in the store.
		/// </returns>
		[NotNull]
		string GetValue([NotNull] string key);

		/// <summary>
		///     Retrieves all key-value pairs for the keys that start with the specified string.
		/// </summary>
		/// <param name="keyPrefix"></param>
		/// <returns></returns>
		[NotNull]
		IEnumerable<KeyValuePair<string, string>> GetRange([NotNull] string keyPrefix);

		/// <summary>
		///     Deletes a key from the key-value store.
		/// </summary>
		/// <param name="key"></param>
		void Delete([NotNull] string key);
	}
}
