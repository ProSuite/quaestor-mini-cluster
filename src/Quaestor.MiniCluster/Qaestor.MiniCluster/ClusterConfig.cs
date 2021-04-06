namespace Quaestor.MiniCluster
{
	public class ClusterConfig
	{
		public string Name { get; set; }

		public int HeartBeatIntervalSeconds { get; set; } = 30;
		public int MemberResponseTimeOutSeconds { get; set; } = 5;

		public int MemberMaxShutdownTimeSeconds { get; set; } = 45;
		public int MemberMaxStartupRetries { get; set; } = 25;
	}
}
