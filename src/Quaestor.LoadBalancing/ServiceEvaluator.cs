using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Health.V1;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;
using Quaestor.KeyValueStore;
using Quaestor.LoadReporting;
using Quaestor.Utilities;

namespace Quaestor.LoadBalancing
{
	public class ServiceEvaluator
	{
		[NotNull] private readonly IComparer<QualifiedService> _serviceComparer;

		[CanBeNull] private readonly string _clientCertificate;

		// NOTE: It is good for performance to re-use channels
		[NotNull] private readonly ConcurrentDictionary<ServiceLocation, Channel> _channelCache;

		private readonly ILogger<ServiceDiscoveryGrpcImpl> _logger =
			Log.CreateLogger<ServiceDiscoveryGrpcImpl>();

		private Exception _lastException;

		public ServiceEvaluator(
			[NotNull] IComparer<QualifiedService> serviceComparer,
			[NotNull] ConcurrentDictionary<ServiceLocation, Channel> channelCache,
			[CanBeNull] string clientCertificate)
		{
			_serviceComparer = serviceComparer;
			_channelCache = channelCache;
			_clientCertificate = clientCertificate;
		}

		public async Task<ConcurrentBag<QualifiedService>> GetHealthyServiceLocations(
			[NotNull] IEnumerable<ServiceLocation> allServices,
			TimeSpan workerResponseTimeout,
			int maxCount = -1)
		{
			var result = new ConcurrentBag<QualifiedService>();
			int healthyCount = 0;

			Stopwatch watch = Stopwatch.StartNew();

			// NOTE: This could be done more in parallel, however it would not be possible to
			//       just break after maxCount and the overall resource consumption would go up.
			foreach (var serviceLocation in allServices)
			{
				if (maxCount > 0 && healthyCount >= maxCount)
				{
					return result;
				}

				bool isHealthy = await IsHealthy(serviceLocation, workerResponseTimeout);

				result.Add(new QualifiedService(serviceLocation, isHealthy));

				if (isHealthy)
				{
					healthyCount++;
				}
			}

			ProcessUtils.EnsureThreadIdInName();

			if (result.Count == 0 && _lastException != null)
			{
				// Something more serious might be wrong and we cannot serve even one location
				_logger.LogWarning(
					"No service can be served AND one or more exceptions occurred! Throwing last exception...");

				throw _lastException;
			}

			watch.Stop();

			_logger.LogDebug("Found {healthyCount} healthy services in {seconds}ms.",
				result.Count, watch.ElapsedMilliseconds);

			return result;
		}

		public async Task<ConcurrentBag<QualifiedService>> GetLoadQualifiedServices(
			[NotNull] IEnumerable<ServiceLocation> serviceLocations,
			TimeSpan workerResponseTimeout)
		{
			Stopwatch watch = Stopwatch.StartNew();

			var result = new ConcurrentBag<QualifiedService>();

			int failureCount = 0;

			var getReportTasks = serviceLocations.Select(serviceLocation =>
				TryAddLoadReport(serviceLocation, workerResponseTimeout, result));

			var allReportRetrievals = await Task.WhenAll(getReportTasks);

			watch.Stop();

			ProcessUtils.EnsureThreadIdInName();

			_logger.LogDebug("Received {loadReportCount} load reports in {seconds}ms.",
				allReportRetrievals.Count(r => r), watch.ElapsedMilliseconds);

			if (result.Count == 0 && allReportRetrievals.Any(r => r == false) &&
			    _lastException != null)
			{
				// Something more serious might be wrong and we cannot serve even one location
				_logger.LogWarning(
					"No service can be served AND {failureCount} exceptions occurred! Throwing last exception...",
					failureCount);

				throw _lastException;
			}

			return result;
		}

		public IList<QualifiedService> Prioritize(
			ConcurrentBag<QualifiedService> loadReports)
		{
			Dictionary<string, double> hostLoads = GetAggregatedHostUtilization(loadReports);

			IList<QualifiedService> result =
				ServicesOrderedByPriority(loadReports, hostLoads, _serviceComparer);

			return result;
		}

		private Dictionary<string, double> GetAggregatedHostUtilization(
			[NotNull] ConcurrentBag<QualifiedService> qualifiedServiceLocations)
		{
			Dictionary<string, List<QualifiedService>> locationsByHost =
				qualifiedServiceLocations
					.GroupBy(s => s.ServiceLocation.HostName)
					.ToDictionary(grouping => grouping.Key, grouping => grouping.ToList());

			Dictionary<string, double> hostLoads = new Dictionary<string, double>();

			// Get the priority of the host machine
			foreach (var hostLocations in locationsByHost)
			{
				List<QualifiedService> locations = hostLocations.Value;

				int currentRequests = 0;
				int totalRequestCapacity = 0;
				foreach (QualifiedService location in locations)
				{
					ServerStats serverStats = location.ServerStats;

					if (serverStats != null)
					{
						currentRequests += serverStats.CurrentRequests;
						totalRequestCapacity += serverStats.RequestCapacity;
					}
				}

				if (totalRequestCapacity == 0)
				{
					hostLoads.Add(hostLocations.Key, double.NaN);
				}
				else
				{
					hostLoads.Add(hostLocations.Key,
						(double) currentRequests / totalRequestCapacity);
				}
			}

			foreach (KeyValuePair<string, double> loadByServer in hostLoads)
			{
				_logger.LogDebug("Aggregated load for host {host}: {load}",
					loadByServer.Key, loadByServer.Value);
			}

			return hostLoads;
		}

		private static IList<QualifiedService> ServicesOrderedByPriority(
			ConcurrentBag<QualifiedService> qualifiedServices,
			IReadOnlyDictionary<string, double> hostLoads,
			IComparer<QualifiedService> comparer)
		{
			foreach (var qualifiedLocation in qualifiedServices)
			{
				if (qualifiedLocation.ServerStats == null)
				{
					continue;
				}

				qualifiedLocation.ServerStats.ServerUtilization =
					hostLoads[qualifiedLocation.ServiceLocation.HostName];
			}

			var orderedLocations =
				qualifiedServices.OrderBy(qs => qs, comparer).ToList();

			return orderedLocations;
		}

		private async Task<bool> TryAddLoadReport(
			[NotNull] ServiceLocation serviceLocation,
			TimeSpan workerResponseTimeout,
			[NotNull] ConcurrentBag<QualifiedService> loadReportsByService)
		{
			Channel channel = GetChannel(serviceLocation);

			try
			{
				// Check for health first, it might be that the process is running but we have not
				// started it.
				if (!await IsHealthy(serviceLocation, channel, workerResponseTimeout))
					return false;

				LoadReportResponse loadReportResponse =
					await GetLoadReport(serviceLocation, channel, workerResponseTimeout);

				ProcessUtils.EnsureThreadIdInName();

				if (loadReportResponse.ServerStats.RequestCapacity == 0)
				{
					_logger.LogDebug(
						"Service location {serviceLocation} reports 0 capacity. It is ignored.",
						serviceLocation);

					return false;
				}

				var qualifiedLocation =
					new QualifiedService(serviceLocation, loadReportResponse.ServerStats)
					{
						KnownLoadRate = loadReportResponse.KnownLoadRate
					};

				loadReportsByService.Add(qualifiedLocation);

				return true;
			}
			catch (TimeoutException)
			{
				_logger.LogDebug(
					"Service location {serviceLocation} took longer than {timeout}s. It is ignored.",
					serviceLocation, workerResponseTimeout.TotalSeconds);
			}
			catch (Exception e)
			{
				_logger.LogWarning(e,
					"Error checking service health / load report for {serviceLocation}",
					serviceLocation);

				_lastException = e;
			}

			return false;
		}

		private async Task<bool> IsHealthy(ServiceLocation serviceLocation,
		                                   TimeSpan workerResponseTimeout)
		{
			bool isHealthy = false;
			try
			{
				Channel channel = GetChannel(serviceLocation);

				isHealthy = await IsHealthy(serviceLocation, channel, workerResponseTimeout);
			}
			catch (TimeoutException)
			{
				_logger.LogDebug(
					"Service location {serviceLocation} took longer than {timeout}s. It is ignored.",
					serviceLocation, workerResponseTimeout.TotalSeconds);
			}
			catch (Exception e)
			{
				_logger.LogWarning(e,
					"Error checking service health / load report for {serviceLocation}",
					serviceLocation);

				_lastException = e;
			}

			return isHealthy;
		}

		private async Task<bool> IsHealthy(ServiceLocation serviceLocation, Channel channel,
		                                   TimeSpan workerResponseTimeout)
		{
			Health.HealthClient healthClient = new Health.HealthClient(channel);

			StatusCode healthStatus = await TaskUtils.TimeoutAfter(
				GrpcUtils.IsServingAsync(healthClient, serviceLocation.ServiceName),
				workerResponseTimeout);

			if (healthStatus != StatusCode.OK)
			{
				_logger.LogDebug(
					"Service location {serviceLocation} is unhealthy and will not be used.",
					serviceLocation);

				return false;
			}

			return true;
		}

		[NotNull]
		private static async Task<LoadReportResponse> GetLoadReport(
			[NotNull] ServiceLocation serviceLocation,
			Channel channel,
			TimeSpan timeout)
		{
			var loadRequest = new LoadReportRequest
			{
				Scope = serviceLocation.Scope,
				ServiceName = serviceLocation.ServiceName
			};

			LoadReportingGrpc.LoadReportingGrpcClient loadClient =
				new LoadReportingGrpc.LoadReportingGrpcClient(channel);

			LoadReportResponse loadReportResponse = await TaskUtils.TimeoutAfter(
				GetLoadReport(loadClient, loadRequest), timeout);

			return loadReportResponse;
		}

		private static async Task<LoadReportResponse> GetLoadReport(
			[NotNull] LoadReportingGrpc.LoadReportingGrpcClient loadClient,
			[NotNull] LoadReportRequest loadRequest)
		{
			return await loadClient.ReportLoadAsync(loadRequest);
		}

		private Channel GetChannel(ServiceLocation serviceLocation)
		{
			if (!_channelCache.TryGetValue(serviceLocation, out Channel channel))
			{
				ChannelCredentials channelCredentials = GrpcUtils.CreateChannelCredentials(
					serviceLocation.UseTls, _clientCertificate);

				channel = GrpcUtils.CreateChannel(serviceLocation.HostName, serviceLocation.Port,
					channelCredentials);

				if (!_channelCache.TryAdd(serviceLocation, channel))
				{
					// It's been added in the meanwhile by another request on another thread
					channel = _channelCache[serviceLocation];
				}
			}

			return channel;
		}
	}
}
