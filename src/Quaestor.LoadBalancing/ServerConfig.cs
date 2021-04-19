using JetBrains.Annotations;

namespace Quaestor.LoadBalancing
{
	[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
	public class ServerConfig
	{
		public string HostName { get; set; }

		public int Port { get; set; }

		public string Certificate { get; set; }

		public string PrivateKeyFile { get; set; }

		public bool EnforceMutualTls { get; set; }
	}
}