using System.Collections.Generic;

namespace Quaestor.MiniCluster.Guardian
{
	public class AgentConfiguration
	{
		public string AgentType { get; set; }

		public string ExecutablePath { get; set; }

		public string CommandLineArguments { get; set; }

		public string HostName { get; set; }

		public int ProcessCount { get; set; }

		public List<int> Ports { get; set; }

		public IList<string> ServiceNames { get; set; }
	}
}
