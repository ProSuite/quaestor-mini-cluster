using System;
using JetBrains.Annotations;
using Quaestor.KeyValueStore;
using Quaestor.LoadReporting;

namespace Quaestor.LoadBalancing
{
	public class QualifiedService
	{
		protected bool Equals(QualifiedService other)
		{
			return Equals(ServiceLocation, other.ServiceLocation);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj))
				return false;
			if (ReferenceEquals(this, obj))
				return true;
			if (obj.GetType() != GetType())
				return false;
			return Equals((QualifiedService) obj);
		}

		public override int GetHashCode()
		{
			return (ServiceLocation.GetHashCode());
		}

		[NotNull] public ServiceLocation ServiceLocation { get; }

		public bool IsHealthy { get; set; }

		[CanBeNull] public ServerStats ServerStats { get; }

		public QualifiedService([NotNull] ServiceLocation serviceLocation,
		                        bool isHealthy)
		{
			ServiceLocation = serviceLocation;
			IsHealthy = isHealthy;
		}

		public QualifiedService([NotNull] ServiceLocation serviceLocation,
		                        ServerStats serverStats,
		                        long reportTimeTicks)
		{
			ServiceLocation = serviceLocation;
			ServerStats = serverStats;

			ReportDate = new TimeSpan(reportTimeTicks);

			IsHealthy = ServerStats != null;
		}

		public TimeSpan ReportDate { get; set; }

		public double KnownLoadRate { get; set; } = -1;

		public DateTime? LastUsed { get; set; }
	}
}
