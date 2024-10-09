using System;
using System.Diagnostics;
using System.Threading.Tasks;
using dotnet_etcd;
using NUnit.Framework;
using Quaestor.Utilities;

namespace Quaestor.KeyValueStore.Tests
{
	[TestFixture]
	public class EtcdKeyValueStoreTest
	{
		private Process _etcdProcess;

		[OneTimeSetUp]
		public void SetupFixture()
		{
			// Make sure the directory containing Etcd.exe is in the path. Alternatively run Etcd.exe in a separate console
			_etcdProcess = ProcessUtils.StartProcess("Etcd.exe", null, true, false);
		}

		[OneTimeTearDown]
		public void TeardownFixture()
		{
			_etcdProcess.Kill();
		}

		[Test]
		public async Task CanPutAndRetrieve()
		{
			EtcdKeyValueStore store = await GetEtcdKeyValueStore();

			TestUtils.SinglePutGet(store, "key1", "value1");
			TestUtils.SinglePutGet(store, "key2", "value2");

			// Keys are case-sensitive:
			string notFound = string.Empty;
			TestUtils.AssertGet(store, "KEY1", notFound);
		}

		[Test]
		public async Task CanGetRange()
		{
			EtcdKeyValueStore store = await GetEtcdKeyValueStore();

			TestUtils.AssertCanGetRange(store);
		}

		[Test]
		public async Task CanPutTwice()
		{
			EtcdKeyValueStore etcdStore = await GetEtcdKeyValueStore();

			TestUtils.SinglePutGet(etcdStore, "same_key", "value1");
			TestUtils.SinglePutGet(etcdStore, "same_key", "value2");
		}

		[Test]
		public async Task CanPutEmptyStringValue()
		{
			EtcdKeyValueStore etcdStore = await GetEtcdKeyValueStore();

			TestUtils.SinglePutGet(etcdStore, "key_with_empty_value", string.Empty);

			Assert.Throws<ArgumentNullException>(() =>
			{
				TestUtils.SinglePutGet(etcdStore, "key_with_null_value", null);
			});
		}

		[Test]
		public async Task CannotPutEmptyStringKey()
		{
			EtcdKeyValueStore store = await GetEtcdKeyValueStore();

			Assert.Throws<ArgumentException>(() =>
			{
				TestUtils.SinglePutGet(store, string.Empty, "value");
			});
		}

		[Test]
		public async Task CanGetNonExistingKey()
		{
			EtcdKeyValueStore store = await GetEtcdKeyValueStore();

			TestUtils.AssertGet(store, "nonexistent_key", string.Empty);
		}

		[Test]
		public async Task CanDeleteKey()
		{
			EtcdKeyValueStore store = await GetEtcdKeyValueStore();

			const string key = "key_to_be_deleted";

			TestUtils.SinglePutGet(store, key, "value");

			store.Delete(key);

			TestUtils.AssertGet(store, key, string.Empty);

			// Delete non-existent key:
			store.Delete(key);

			TestUtils.AssertGet(store, key, string.Empty);
		}

		private static async Task<EtcdKeyValueStore> GetEtcdKeyValueStore()
		{
			EtcdClient etcdClient =
				new EtcdClient("http://localhost:2379");

			TimeSpan? timeout2Minutes = new TimeSpan(0, 2, 0);
			var etcdStore = new EtcdKeyValueStore(etcdClient, timeout2Minutes);

			// Warm up:
			Stopwatch watch = Stopwatch.StartNew();
			bool connected = await etcdStore.ConnectAsync();
			watch.Stop();

			Assert.IsTrue(connected);

			Console.WriteLine("Initial connect: {0}ms", watch.ElapsedMilliseconds);
			return etcdStore;
		}
	}
}
