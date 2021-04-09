using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;
using Quaestor.KeyValueStore;
using Quaestor.Microservices.Definitions;

namespace Quaestor.Microservices
{
	public class ServiceDiscoveryGrpcImpl : ServiceDiscoveryGrpc.ServiceDiscoveryGrpcBase
	{
		private readonly ServiceRegistry _serviceRegistry;

		private static readonly Random _random = new Random();
		private bool _lastRequestFailed = false;

		private readonly ILogger<ServiceDiscoveryGrpcImpl> _logger =
			Log.CreateLogger<ServiceDiscoveryGrpcImpl>();

		public ServiceDiscoveryGrpcImpl(ServiceRegistry serviceRegistry)
		{
			_serviceRegistry = serviceRegistry;
		}

		public override Task<LocateServicesResponse> LocateServices(
			LocateServicesRequest request, ServerCallContext context)
		{
			_lastRequestFailed = false;

			LocateServicesResponse response = null;

			try
			{
				response = new LocateServicesResponse();

				response.ServiceLocations.AddRange(GetServiceLocationMessages(request));
			}
			catch (Exception e)
			{
				_lastRequestFailed = true;

				_logger.LogError(e, "Error discovering service {serviceName}", request.ServiceName);
			}

			return Task.FromResult(response);
		}

		public bool IsHealthy()
		{
			return !_lastRequestFailed;
		}

		private IEnumerable<ServiceLocationMsg> GetServiceLocationMessages(
			LocateServicesRequest request)
		{
			var result = new List<ServiceLocationMsg>();

			foreach (ServiceLocation serviceLocation in
				_serviceRegistry.GetServiceLocations(request.ServiceName))
			{
				ServiceLocationMsg serviceLocationMsg = new ServiceLocationMsg
				{
					Scope = serviceLocation.Scope,
					ServiceName = serviceLocation.ServiceName,
					HostName = serviceLocation.HostName,
					Port = serviceLocation.Port
				};

				result.Add(serviceLocationMsg);
			}

			Shuffle(result);

			return result;
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
