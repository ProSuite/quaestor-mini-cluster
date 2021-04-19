using System.Collections.Generic;
using JetBrains.Annotations;

namespace Quaestor.Environment
{
	[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
	public class AgentConfiguration
	{
		/// <summary>
		///     The type of the agent. So far used only for display purposes.
		/// </summary>
		public string AgentType { get; set; }

		/// <summary>
		///     The full path to the executable.
		/// </summary>
		public string ExecutablePath { get; set; }

		/// <summary>
		///     The command line arguments to be used when starting the executable.
		/// </summary>
		public string CommandLineArguments { get; set; }

		public Dictionary<string, string> EnvironmentVariables { get; set; }

		/// <summary>
		///     The name or IP address of the host in which the executable is started.
		/// </summary>
		public string HostName { get; set; }

		/// <summary>
		///     Use transport layer security (SSL/TLS). If the server is using TLS the flag
		///     must also be set by the client.
		/// </summary>
		public bool UseTls { get; set; }

		/// <summary>
		///     For additional client authentication: The client certificate subject or
		///     thumbprint from the certificate store (Current User).
		///     Note that the certificate's private key must be accessible to the executable.
		///     Must be set if the server is configured to require mutual TLS.
		/// </summary>
		public string ClientCertificate { get; set; }

		/// <summary>
		///     The number of processes to be started and maintained by the cluster.
		/// </summary>
		public int ProcessCount { get; set; }

		/// <summary>
		///     The list of ports for the processes to be started. The number of ports must correspond
		///     to <see cref="ProcessCount" /> or remain empty in order to use ephemeral ports. Ephemeral
		///     ports can only be used for services with a local address (i.e. <see cref="HostName" />
		///     being 'localhost' or '127.0.0.1').
		/// </summary>
		[CanBeNull]
		public List<int> Ports { get; set; }

		/// <summary>
		///     The list of services that are hosted by the configured process(es). The health check is
		///     performed for each service name using the GRPC Health Checking Protocol.
		/// </summary>
		[CanBeNull]
		public List<string> ServiceNames { get; set; }
	}
}
