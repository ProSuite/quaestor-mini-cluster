using CommandLine;
using JetBrains.Annotations;

namespace Quaestor.Console
{
	public class QuaestorOptions
	{
		[Option('c', "configDir", Required = false,
			HelpText =
				"The configuration directory containing quaestor.config.yml and optionally the " +
				"log4net.config file.")]
		public string ConfigDirectory { get; set; }
	}

	[Verb("cluster", HelpText = "Starts the Quaestor mini-cluster.")]
	[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
	public class QuaestorClusterOptions : QuaestorOptions
	{
	}

	[Verb("load-balancer", HelpText = "Starts the Quaestor load balancer.")]
	[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
	public class QuaestorLoadBalancerOptions : QuaestorOptions
	{
	}
}
