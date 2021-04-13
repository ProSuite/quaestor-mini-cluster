using JetBrains.Annotations;

namespace Quaestor.KeyValueStore
{
	public class ServiceLocation
	{
		public ServiceLocation([NotNull] string serviceName,
		                       [NotNull] string hostName,
		                       int port,
		                       [CanBeNull] string scope = null)
		{
			ServiceName = serviceName;
			HostName = hostName;
			Port = port;
			Scope = scope;
		}

		public string Scope { get; }

		public string ServiceName { get; }
		public string HostName { get; }
		public int Port { get; }

		protected bool Equals(ServiceLocation other)
		{
			return Scope == other.Scope && ServiceName == other.ServiceName &&
			       HostName == other.HostName && Port == other.Port;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj))
				return false;
			if (ReferenceEquals(this, obj))
				return true;
			if (obj.GetType() != GetType())
				return false;
			return Equals((ServiceLocation) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = (Scope != null ? Scope.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (ServiceName != null ? ServiceName.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (HostName != null ? HostName.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ Port;
				return hashCode;
			}
		}
	}
}
