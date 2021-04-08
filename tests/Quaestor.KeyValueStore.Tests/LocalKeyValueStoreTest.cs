using System;
using NUnit.Framework;

namespace Quaestor.KeyValueStore.Tests
{
	[TestFixture]
	public class LocalKeyValueStoreTest
	{
		[Test]
		public void CanPutAndRetrieve()
		{
			LocalKeyValueStore store = new LocalKeyValueStore();

			TestUtils.SinglePutGet(store, "key1", "value1");
			TestUtils.SinglePutGet(store, "key2", "value2");

			// Keys are case-sensitive:
			string notFound = string.Empty;
			TestUtils.AssertGet(store, "KEY1", notFound);
		}

		[Test]
		public void CanGetRange()
		{
			LocalKeyValueStore store = new LocalKeyValueStore();

			TestUtils.AssertCanGetRange(store);
		}

		[Test]
		public void CanPutKeyTwice()
		{
			LocalKeyValueStore store = new LocalKeyValueStore();

			TestUtils.SinglePutGet(store, "key1", "value1");
			TestUtils.SinglePutGet(store, "key1", "value2");
		}

		[Test]
		public void CanPutEmptyString()
		{
			LocalKeyValueStore store = new LocalKeyValueStore();

			TestUtils.SinglePutGet(store, "key1", string.Empty);

			Assert.Throws<ArgumentNullException>(() =>
			{
				TestUtils.SinglePutGet(store, "key1", null);
			});
		}

		[Test]
		public void CannotPutEmptyStringKey()
		{
			LocalKeyValueStore store = new LocalKeyValueStore();

			Assert.Throws<ArgumentException>(() =>
			{
				TestUtils.SinglePutGet(store, string.Empty, "value");
			});
		}

		[Test]
		public void CanGetNonExistingKey()
		{
			LocalKeyValueStore store = new LocalKeyValueStore();

			TestUtils.AssertGet(store, "noes not exist", string.Empty);
		}

		[Test]
		public void CanDeleteKey()
		{
			LocalKeyValueStore store = new LocalKeyValueStore();

			const string key = "key_to_be_deleted";

			TestUtils.SinglePutGet(store, key, "value");

			store.Delete(key);

			TestUtils.AssertGet(store, key, string.Empty);

			// Delete non-existent key:
			store.Delete(key);

			TestUtils.AssertGet(store, key, string.Empty);
		}
	}
}
