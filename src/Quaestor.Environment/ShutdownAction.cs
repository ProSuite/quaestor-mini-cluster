namespace Quaestor.Environment
{
	public enum ShutdownAction
	{
		/// <summary>
		///     No action is performed and the process keeps running.
		/// </summary>
		None = 0,

		/// <summary>
		///     The process is killed if it was started by the cluster. If the process was not started
		///     by the cluster, it is not killed (for symmetry reasons).
		/// </summary>
		Kill = 1,

		//Shutdown using grpc service (not yet implemented)
	}
}
