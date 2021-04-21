using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.HealthCheck;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;
using Quaestor.KeyValueStore;
using Quaestor.LoadReporting;
using Quaestor.ServiceDiscovery;
using Quaestor.Utilities;

namespace Quaestor.LoadBalancing
{
	public class ServiceDiscoveryGrpcImpl : ServiceDiscoveryGrpc.ServiceDiscoveryGrpcBase
	{
		private readonly ServiceRegistry _serviceRegistry;
		[CanBeNull] private readonly string _clientCertificate;

		private readonly IDictionary<ServiceLocation, Channel> _channels =
			new Dictionary<ServiceLocation, Channel>();

		private static readonly Random _random = new Random();

		private readonly ILogger<ServiceDiscoveryGrpcImpl> _logger =
			Log.CreateLogger<ServiceDiscoveryGrpcImpl>();

		public ServiceDiscoveryGrpcImpl(
			[NotNull] ServiceRegistry serviceRegistry,
			[CanBeNull] string clientCertificate = null)
		{
			_serviceRegistry = serviceRegistry;

			_clientCertificate = clientCertificate;
		}

		public override Task<DiscoverServicesResponse> DiscoverServices(
			DiscoverServicesRequest request, ServerCallContext context)
		{
			DiscoverServicesResponse response = null;

			try
			{
				response = new DiscoverServicesResponse();

				IList<ServiceLocationMsg> serviceLocationMessages = GetRandomServiceLocationMessages(request);

				response.ServiceLocations.AddRange(serviceLocationMessages);
			}
			catch (Exception e)
			{
				_logger.LogError(e, "Error discovering service {serviceName}", request.ServiceName);

				SetUnhealthy();
			}

			return Task.FromResult(response);
		}

		public override async Task<DiscoverServicesResponse> DiscoverTopServices(
			DiscoverServicesRequest request, ServerCallContext context)
		{
			DiscoverServicesResponse response = null;

			try
			{
				response = new DiscoverServicesResponse();

				IList<ServiceLocationMsg> result = await GetTopServiceLocationMessages(request);

				response.ServiceLocations.AddRange(result);
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

		[CanBeNull]
		public HealthServiceImpl Health { get; set; }

		private void SetUnhealthy()
		{
			Health?.SetStatus(ServiceName, HealthCheckResponse.Types.ServingStatus.NotServing);
		}

		private IList<ServiceLocationMsg> GetRandomServiceLocationMessages(
			[NotNull] DiscoverServicesRequest request)
		{
			List<ServiceLocation> allServices = GetShuffledServices(request);

			IList<ServiceLocationMsg> result = ToServiceLocationMessages(
				GetHealthyServiceLocations(allServices, request.MaxCount)).ToList();

			return result;
		}

		private async Task<IList<ServiceLocationMsg>> GetTopServiceLocationMessages(
			[NotNull] DiscoverServicesRequest request)
		{
			// Shuffle first because typically many will have rank 0 -> try not to return the same one for concurrent requests
			List<ServiceLocation> allServices = GetShuffledServices(request);

			IList<ServiceLocation> servicesByDesirability =
				await GetServicesRankedByLoad(allServices, request.MaxCount);

			int maxResultCount = request.MaxCount == 0 ? -1 : request.MaxCount;

			IList<ServiceLocationMsg> result =
				ToServiceLocationMessages(servicesByDesirability, maxResultCount).ToList();

			return result;
		}

		private IEnumerable<ServiceLocation> GetHealthyServiceLocations(
			[NotNull] IList<ServiceLocation> allServices,
			int maxCount = -1)
		{
			int yieldCount = 0;

			foreach (var serviceLocation in allServices)
			{
				if (maxCount > 0 && yieldCount >= maxCount)
				{
					yield break;
				}

				if (IsServiceHealthy(serviceLocation))
				{
					yield return serviceLocation;
					yieldCount++;
				}
			}
		}

		private bool IsServiceHealthy(ServiceLocation serviceLocation)
		{
			ChannelBase channel = GetChannel(serviceLocation);

			Health.HealthClient healthClient = new Health.HealthClient(channel);

			bool serving = GrpcUtils.IsServing(healthClient, serviceLocation.ServiceName,
				out StatusCode statusCode);

			if (serving)
			{
				return true;
			}

			_logger.LogWarning(
				"Service location {location} is unhealthy ({status}). It will be removed from the service registry!",
				serviceLocation, statusCode);

			// In case it is restarted (or only temporarily offline) the service will be re-registered by the cluster...
			_serviceRegistry.EnsureRemoved(serviceLocation.ServiceName, serviceLocation.HostName,
				serviceLocation.Port, serviceLocation.UseTls);

			return false;
		}

		private ChannelBase GetChannel(ServiceLocation serviceLocation)
		{
			if (!_channels.TryGetValue(serviceLocation, out Channel channel))
			{
				ChannelCredentials channelCredentials = GrpcUtils.CreateChannelCredentials(
					serviceLocation.UseTls, _clientCertificate);

				channel = GrpcUtils.CreateChannel(serviceLocation.HostName, serviceLocation.Port,
					channelCredentials);

				_channels.Add(serviceLocation, channel);
			}

			return channel;
		}

		private static IEnumerable<ServiceLocationMsg> ToServiceLocationMessages(
			IEnumerable<ServiceLocation> serviceLocations,
			int maxCount = -1)
		{
			int resultCount = 0;

			foreach (ServiceLocation serviceLocation in serviceLocations)
			{
				if (maxCount > 0 && resultCount++ >= maxCount)
				{
					yield break;
				}

				ServiceLocationMsg serviceLocationMsg = new ServiceLocationMsg
				{
					Scope = serviceLocation.Scope,
					ServiceName = serviceLocation.ServiceName,
					HostName = serviceLocation.HostName,
					Port = serviceLocation.Port
				};

				yield return serviceLocationMsg;
			}
		}



		private List<ServiceLocation> GetShuffledServices(DiscoverServicesRequest request)
		{
			List<ServiceLocation> allServices =
				_serviceRegistry.GetServiceLocations(request.ServiceName).ToList();

			_logger.LogDebug("{count} {serviceName} service(s) found in service registry:",
				allServices.Count, request.ServiceName);

			Shuffle(allServices);

			foreach (ServiceLocation serviceLocation in allServices)
			{
				_logger.LogDebug("  {serviceLocation}", serviceLocation);
			}

			return allServices;
		}

		private async Task<IList<ServiceLocation>> GetServicesRankedByLoad(
			[NotNull] IEnumerable<ServiceLocation> serviceLocations,
			int maxCount)
		{
			if (maxCount <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(maxCount));
			}

			Dictionary<ServiceLocation, double> servicesByDesirability =
				new Dictionary<ServiceLocation, double>();

			var freeList = new List<ServiceLocation>();

			foreach (var serviceLocation in serviceLocations)
			{
				if (freeList.Count >= maxCount)
				{
					// Do not differentiate between free services - it's not worth the effort (or is it?)
					return freeList;
				}

				if (!IsServiceHealthy(serviceLocation))
				{
					continue;
				}

				double desirability = await GetServiceRank(serviceLocation);

				if (desirability < 0)
				{
					continue;
				}

				if (desirability == 0)
				{
					freeList.Add(serviceLocation);
				}

				servicesByDesirability.Add(serviceLocation, desirability);
			}

			return servicesByDesirability.Keys.OrderBy(sl => servicesByDesirability[sl]).ToList();
		}

		/// <summary>
		///     Returns a ranking number for the specified service location. Lower is
		///     better as long as the rank is positive.
		///     Locations that report 0 request capacity, -1 is returned.
		///     For locations reporting 0 current requests, 0 is returned. These services
		///     are all considered equally good. Requests with at least one ongoing request
		///     are weighted by the CPU load of the service process.
		/// </summary>
		/// <param name="serviceLocation"></param>
		/// <returns></returns>
		private async Task<double> GetServiceRank(
			[NotNull] ServiceLocation serviceLocation)
		{
			var loadRequest = new LoadReportRequest
			{
				Scope = serviceLocation.Scope,
				ServiceName = serviceLocation.ServiceName
			};

			LoadReportingGrpc.LoadReportingGrpcClient loadClient =
				new LoadReportingGrpc.LoadReportingGrpcClient(GetChannel(serviceLocation));

			LoadReportResponse loadReportResponse =
				await loadClient.ReportLoadAsync(loadRequest);

			if (loadReportResponse.KnownLoadRate >= 0)
			{
				return loadReportResponse.KnownLoadRate;
			}

			int capacity = loadReportResponse.ServerStats.RequestCapacity;

			if (capacity == 0)
			{
				return -1;
			}

			double workload = (double) loadReportResponse.ServerStats.CurrentRequests /
			                  capacity;

			double cpuUsage = loadReportResponse.ServerStats.ServerUtilization;

			var desirability = cpuUsage > 0 ? workload * cpuUsage : workload;

			return desirability;
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
