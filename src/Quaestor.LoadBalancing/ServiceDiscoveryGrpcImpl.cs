using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.HealthCheck;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;
using Quaestor.KeyValueStore;
using Quaestor.ServiceDiscovery;

namespace Quaestor.LoadBalancing
{
	public class ServiceDiscoveryGrpcImpl : ServiceDiscoveryGrpc.ServiceDiscoveryGrpcBase
	{
		private readonly ServiceRegistry _serviceRegistry;
		[CanBeNull] private readonly string _clientCertificate;

		private readonly ConcurrentDictionary<ServiceLocation, Channel> _channels =
			new ConcurrentDictionary<ServiceLocation, Channel>();

		private readonly ConcurrentQueue<QualifiedService> _recentlyUsedServices =
			new ConcurrentQueue<QualifiedService>();

		private static readonly Random _random = new Random();

		private readonly ILogger<ServiceDiscoveryGrpcImpl> _logger =
			Log.CreateLogger<ServiceDiscoveryGrpcImpl>();

		private TimeSpan WorkerResponseTimeout =>
			TimeSpan.FromSeconds(WorkerResponseTimeoutSeconds);

		public ServiceDiscoveryGrpcImpl(
			[NotNull] ServiceRegistry serviceRegistry,
			[CanBeNull] string clientCertificate = null)
		{
			_serviceRegistry = serviceRegistry;

			_clientCertificate = clientCertificate;
		}

		public override async Task<DiscoverServicesResponse> DiscoverServices(
			DiscoverServicesRequest request, ServerCallContext context)
		{
			DiscoverServicesResponse response = null;

			try
			{
				Stopwatch watch = Stopwatch.StartNew();

				response = new DiscoverServicesResponse();

				IList<ServiceLocationMsg> serviceLocationMessages =
					await GetHealthyServiceLocationMessages(request);

				response.ServiceLocations.AddRange(serviceLocationMessages);

				_logger.LogDebug("Returning {count} service location(s) [{time}ms]",
					serviceLocationMessages.Count, watch.ElapsedMilliseconds);
			}
			catch (Exception e)
			{
				_logger.LogError(e, "Error discovering service {serviceName}", request.ServiceName);

				SetUnhealthy();
			}

			return response;
		}

		public override async Task<DiscoverServicesResponse> DiscoverTopServices(
			DiscoverServicesRequest request, ServerCallContext context)
		{
			DiscoverServicesResponse response = null;

			try
			{
				Stopwatch watch = Stopwatch.StartNew();

				response = new DiscoverServicesResponse();

				IList<ServiceLocationMsg> result = await GetTopServiceLocationMessages(request);

				response.ServiceLocations.AddRange(result);

				_logger.LogDebug("Returning {count} service location(s) [{time}ms]", result.Count,
					watch.ElapsedMilliseconds);
			}
			catch (Exception e)
			{
				_logger.LogError(e, "Error discovering service {serviceName}", request.ServiceName);

				SetUnhealthy();
			}

			return response;
		}

		/// <summary>
		///     The name of this service: ServiceDiscoveryGrpc
		///     It can be checked using the grpc Health Check service.
		/// </summary>
		public string ServiceName => nameof(ServiceDiscoveryGrpc);

		/// <summary>
		///     The health check service to be updated in case this service becomes unhealthy.
		/// </summary>
		[CanBeNull]
		public HealthServiceImpl Health { get; set; }

		/// <summary>
		///     Whether unhealthy worker services should be removed from the service registry.
		/// </summary>
		public bool RemoveUnhealthyServices { get; set; }

		/// <summary>
		///     The timeout in seconds that is waited for when checking an individual worker service.
		///     It is used both for checking health and getting a load report.
		/// </summary>
		public double WorkerResponseTimeoutSeconds { get; set; } = 2;

		/// <summary>
		///     The time a service that has just been returned to a requester, remains in the least-recently-used
		///     cache which results in a low priority for re-use.
		/// </summary>
		public double RecentlyUsedServiceTimeoutSeconds { get; set; } = 5;

		/// <summary>
		///     The comparer to be used to prioritize the service locations that have been qualified
		///     using load reports.
		/// </summary>
		public IComparer<QualifiedService> ServiceComparer { get; set; } = new LoadReportComparer();

		private void SetUnhealthy()
		{
			Health?.SetStatus(ServiceName, HealthCheckResponse.Types.ServingStatus.NotServing);
		}

		private async Task<IList<ServiceLocationMsg>> GetHealthyServiceLocationMessages(
			[NotNull] DiscoverServicesRequest request)
		{
			// By default the prioritization should be random rather than the order defined in the
			// service registry. TODO: Configurable prioritization:
			// Random (using shuffled services)
			// Round-robin (using least-recently-used list) and based on available / best services
			List<ServiceLocation> allServices = GetServices(request, true);

			_logger.LogDebug("{count} {serviceName} service(s) found in service registry:",
				allServices.Count, request.ServiceName);

			if (allServices.Count == 0)
			{
				return new List<ServiceLocationMsg>(0);
			}

			ServiceEvaluator serviceEvaluator =
				new ServiceEvaluator(ServiceComparer, _channels, _clientCertificate);

			ConcurrentBag<QualifiedService> services =
				await serviceEvaluator.GetHealthyServiceLocations(allServices,
					WorkerResponseTimeout, request.MaxCount);

			foreach (QualifiedService qualifiedService in services)
			{
				if (RemoveUnhealthyServices && !qualifiedService.IsHealthy)
				{
					var unhealthyLocation = qualifiedService.ServiceLocation;

					_serviceRegistry.EnsureRemoved(unhealthyLocation.ServiceName,
						unhealthyLocation.HostName, unhealthyLocation.Port,
						unhealthyLocation.UseTls);
				}
			}

			return ToServiceLocationMessages(services
				.Where(qs => qs.IsHealthy)
				.Select(qs => qs.ServiceLocation)).ToList();
		}

		private async Task<IList<ServiceLocationMsg>> GetTopServiceLocationMessages(
			[NotNull] DiscoverServicesRequest request)
		{
			List<ServiceLocation> allServices = GetServices(request);

			_logger.LogDebug("{count} {serviceName} service(s) found in service registry:",
				allServices.Count, request.ServiceName);

			if (allServices.Count == 0)
			{
				return new List<ServiceLocationMsg>(0);
			}

			ServiceEvaluator serviceEvaluator =
				new ServiceEvaluator(ServiceComparer, _channels, _clientCertificate);

			var qualifiedServices =
				await serviceEvaluator.GetLoadQualifiedServices(allServices,
					WorkerResponseTimeout);

			if (qualifiedServices.Count == 0)
			{
				// Try again with very high time-out:
				qualifiedServices =
					await serviceEvaluator.GetLoadQualifiedServices(allServices,
						TimeSpan.FromSeconds(30));
			}

			_logger.LogDebug("{serviceCount} service location(s) responded with a load report.",
				qualifiedServices.Count);

			if (qualifiedServices.Count == 0)
			{
				return new List<ServiceLocationMsg>(0);
			}

			IList<QualifiedService> orderedServices =
				serviceEvaluator.Prioritize(qualifiedServices);

			ExcludeRecentlyUsed(orderedServices);

			int maxResultCount = request.MaxCount == 0 ? -1 : request.MaxCount;

			IList<ServiceLocationMsg> result =
				UseTopServices(orderedServices, maxResultCount).ToList();

			return result;
		}

		private void ExcludeRecentlyUsed([NotNull] ICollection<QualifiedService> orderedServices)
		{
			TimeSpan recentlyUsedTimeout = TimeSpan.FromSeconds(RecentlyUsedServiceTimeoutSeconds);

			if (orderedServices.Count > 1 && !_recentlyUsedServices.IsEmpty)
			{
				DequeueNonRecentlyUsed(_recentlyUsedServices, recentlyUsedTimeout);

				foreach (QualifiedService orderedLocation in orderedServices.ToList())
				{
					if (_recentlyUsedServices.Contains(orderedLocation))
					{
						// Move it to the end:
						orderedServices.Remove(orderedLocation);
						orderedServices.Add(orderedLocation);

						_logger.LogDebug(
							"{serviceLocation} has been used less than {timeout} ago and is moved to the end of the priority list.",
							orderedLocation.ServiceLocation, recentlyUsedTimeout);
					}
				}
			}
		}

		private static void DequeueNonRecentlyUsed(
			[NotNull] ConcurrentQueue<QualifiedService> services,
			TimeSpan timeout)
		{
			// Filter previously (i.e. in the last few seconds) returned services:
			// To avoid races 
			while (services.TryPeek(out QualifiedService previous))
			{
				if (previous.LastUsed == null)
					throw new InvalidOperationException(
						"Used service location has no LastUsed date");

				if (previous.LastUsed.Value + timeout < DateTime.Now)
				{
					services.TryDequeue(out _);
				}
				else
				{
					return;
				}
			}
		}

		private IEnumerable<ServiceLocationMsg> UseTopServices(
			IEnumerable<QualifiedService> qualifiedServices,
			int maxCount = -1)
		{
			int resultCount = 0;

			foreach (QualifiedService qualifiedService in qualifiedServices)
			{
				if (maxCount > 0 && resultCount >= maxCount)
				{
					yield break;
				}

				ServiceLocationMsg serviceLocationMsg =
					ToServiceLocationMsg(qualifiedService.ServiceLocation);

				// And add it to the least-recently-used queue
				qualifiedService.LastUsed = DateTime.Now;
				_recentlyUsedServices.Enqueue(qualifiedService);

				yield return serviceLocationMsg;

				resultCount++;
			}
		}

		private static IEnumerable<ServiceLocationMsg> ToServiceLocationMessages(
			IEnumerable<ServiceLocation> serviceLocations,
			int maxCount = -1)
		{
			int resultCount = 0;

			foreach (ServiceLocation serviceLocation in serviceLocations)
			{
				if (maxCount > 0 && resultCount >= maxCount)
				{
					yield break;
				}

				ServiceLocationMsg serviceLocationMsg = ToServiceLocationMsg(serviceLocation);

				yield return serviceLocationMsg;

				resultCount++;
			}
		}

		private static ServiceLocationMsg ToServiceLocationMsg(ServiceLocation serviceLocation)
		{
			ServiceLocationMsg serviceLocationMsg = new ServiceLocationMsg
			{
				Scope = serviceLocation.Scope,
				ServiceName = serviceLocation.ServiceName,
				HostName = serviceLocation.HostName,
				Port = serviceLocation.Port
			};

			return serviceLocationMsg;
		}

		private List<ServiceLocation> GetServices([NotNull] DiscoverServicesRequest request,
		                                          bool shuffled = false)
		{
			List<ServiceLocation> allServices =
				_serviceRegistry.GetServiceLocations(request.ServiceName).ToList();

			_logger.LogDebug("{count} {serviceName} service(s) found in service registry:",
				allServices.Count, request.ServiceName);

			if (shuffled)
			{
				Shuffle(allServices);
			}

			foreach (ServiceLocation serviceLocation in allServices)
			{
				_logger.LogDebug("  {serviceLocation}", serviceLocation);
			}

			return allServices;
		}

		private static void Shuffle<T>(IList<T> list)
		{
			// https://stackoverflow.com/questions/273313/randomize-a-listt:

			int n = list.Count;
			while (n > 1)
			{
				n--;
				int k = _random.Next(n + 1);
				T value = list[k];
				list[k] = list[n];
				list[n] = value;
			}
		}
	}
}
