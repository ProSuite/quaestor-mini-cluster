using CommandLine;
using JetBrains.Annotations;

namespace Quaestor.Cluster.Console
{
	[UsedImplicitly]
	public class QuaestorClusterOptions
	{
		[Option('c', "configDir", Required = false,
			HelpText =
				"The configuration directory containing quaestor.config.yml and optionally the " +
				"log4net.config file.")]
		public string ConfigDirectory { get; set; }
	}
}
