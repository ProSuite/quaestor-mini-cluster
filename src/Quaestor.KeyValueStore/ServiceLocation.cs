namespace Quaestor.KeyValueStore
{
	public class ServiceLocation
	{
		public ServiceLocation(string scope = null)
		{
			Scope = scope;
		}

		public string Scope { get; set; }

		public string ServiceName { get; set; }
		public string HostName { get; set; }
		public int Port { get; set; }
	}
}
