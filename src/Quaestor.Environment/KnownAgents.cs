using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Quaestor.Environment
{
	public static class KnownAgents
	{
		const string _agentSectionConfigKey = "agents";

		public static IList<AgentConfiguration> AgentConfigurations { get; } =
			new List<AgentConfiguration>();

		public static void ConfigureAgents(IConfiguration configuration)
		{
			Dictionary<string, AgentConfiguration> dictionary =
				configuration.GetSection(_agentSectionConfigKey)
					.Get<Dictionary<string, AgentConfiguration>>();

			if (dictionary == null)
			{
				throw new InvalidOperationException(
					$"{_agentSectionConfigKey} section not found in configuration");
			}

			List<AgentConfiguration> agentConfigs =
				dictionary.Select(kvp =>
				{
					kvp.Value.AgentType = kvp.Key;
					return kvp.Value;
				}).ToList();

			Configure(agentConfigs);
		}

		public static List<AgentConfiguration> Configure(IConfiguration configuration)
		{
			Dictionary<string, AgentConfiguration> dictionary =
				configuration.GetSection(_agentSectionConfigKey)
					.Get<Dictionary<string, AgentConfiguration>>();

			if (dictionary == null)
			{
				throw new InvalidOperationException(
					$"{_agentSectionConfigKey} section not found in configuration");
			}

			List<AgentConfiguration> agentConfigs =
				dictionary.Select(kvp =>
				{
					kvp.Value.AgentType = kvp.Key;
					return kvp.Value;
				}).ToList();

			Configure(agentConfigs);

			return agentConfigs;
		}

		private static void Configure(IEnumerable<AgentConfiguration> agentConfigurations)
		{
			AgentConfigurations.Clear();

			foreach (var agentConfiguration in agentConfigurations)
			{
				AgentConfigurations.Add(agentConfiguration);
			}
		}

		public static IEnumerable<AgentConfiguration> Get()
		{
			return AgentConfigurations;
		}

		// ReSharper disable once EntityNameCapturedOnly.Global
		public static IEnumerable<AgentConfiguration> Get(WellKnownAgentType type)
		{
			return Get().Where(a => a.AgentType.Equals(type.ToString()));
		}
	}
}
