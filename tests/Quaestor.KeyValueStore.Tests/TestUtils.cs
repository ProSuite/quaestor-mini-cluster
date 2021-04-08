using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;

namespace Quaestor.KeyValueStore.Tests
{
	internal static class TestUtils
	{
		public static void SinglePutGet(IKeyValueStore store,
		                                string key, string value)
		{
			Stopwatch watch = Stopwatch.StartNew();

			store.Put(key, value);

			watch.Stop();

			Console.WriteLine("Put {0} | {1}: {2}ms", key, value, watch.ElapsedMilliseconds);

			AssertGet(store, key, value);
		}

		public static void AssertGet(IKeyValueStore store, string key, string expectedValue)
		{
			var watch = Stopwatch.StartNew();

			string returned = store.GetValue(key);

			watch.Stop();

			Assert.AreEqual(expectedValue, returned);

			if (string.Empty == returned)
			{
				returned = "<empty string>";
			}

			Console.WriteLine("Get {0} | {1}: {2}ms", key, returned, watch.ElapsedMilliseconds);
		}

		public static void AssertCanGetRange(IKeyValueStore store)
		{
			SinglePutGet(store, "range_key1", "range_value1");
			SinglePutGet(store, "range_key2", "range_value2");

			List<KeyValuePair<string, string>> keyValuePairs = store.GetRange("range").ToList();

			Assert.AreEqual(2, keyValuePairs.Count);

			var key1List = keyValuePairs.Where(kvp => kvp.Key == "range_key1").ToList();
			Assert.AreEqual(1, key1List.Count);
			Assert.AreEqual("range_value1", key1List[0].Value);

			var key2List = keyValuePairs.Where(kvp => kvp.Key == "range_key2").ToList();
			Assert.AreEqual(1, key2List.Count);
			Assert.AreEqual("range_value2", key2List[0].Value);
		}
	}
}
