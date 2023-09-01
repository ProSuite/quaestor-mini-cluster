using CommandLine;
using JetBrains.Annotations;

namespace Quaestor.LoadBalancer.Console
{
	[UsedImplicitly]
	public class QuaestorLoadBalancerOptions
	{
		[Option('c', "configDir", Required = false,
			HelpText =
				"The configuration directory containing quaestor.config.yml and optionally the " +
				"log4net.config file.")]
		public string ConfigDirectory { get; set; }
	}
}
