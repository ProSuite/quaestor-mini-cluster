using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Grpc.Core;
using Grpc.Health.V1;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;

namespace Quaestor.Utilities
{
	/// <summary>
	///     Utility methods mostly copied from ProSuite.Microservices.Client. Could be in a shared package.
	/// </summary>
	public static class GrpcUtils
	{
		private static readonly ILogger _logger = Log.CreateLogger("GrpcUtils");

		public static IList<ChannelOption> CreateChannelOptions(int maxMessageLength)
		{
			if (maxMessageLength == 0) return new List<ChannelOption>();

			var maxMsgSendLengthOption = new ChannelOption(
				"grpc.max_send_message_length", maxMessageLength);

			var maxMsgReceiveLengthOption = new ChannelOption(
				"grpc.max_receive_message_length", maxMessageLength);

			var channelOptions = new List<ChannelOption>
			{
				maxMsgSendLengthOption,
				maxMsgReceiveLengthOption
			};

			return channelOptions;
		}

		public static ChannelCredentials CreateChannelCredentials(
			bool useTls,
			[CanBeNull] string clientCertificate = null)
		{
			if (!useTls)
			{
				_logger.LogDebug("Using insecure channel credentials");

				return ChannelCredentials.Insecure;
			}

			string rootCertificatesAsPem =
				CertificateUtils.GetUserRootCertificatesInPemFormat();

			KeyCertificatePair sslClientCertificate = null;
			if (!string.IsNullOrEmpty(clientCertificate))
			{
				KeyPair keyPair = CertificateUtils.FindKeyCertificatePairFromStore(
					clientCertificate, new[]
					{
						X509FindType.FindBySubjectDistinguishedName,
						X509FindType.FindByThumbprint,
						X509FindType.FindBySubjectName
					}, StoreName.My, StoreLocation.CurrentUser);

				if (keyPair != null)
				{
					_logger.LogDebug("Using client-side certificate");

					sslClientCertificate =
						new KeyCertificatePair(keyPair.PublicKey, keyPair.PrivateKey);
				}
				else
				{
					throw new ArgumentException(
						$"Could not usable find client certificate {clientCertificate} in certificate store.");
				}
			}

			var result = new SslCredentials(rootCertificatesAsPem, sslClientCertificate);

			return result;
		}

		public static Channel CreateChannel(
			string host, int port,
			ChannelCredentials credentials,
			int maxMessageLength = 0)
		{
			_logger.LogDebug($"Creating channel to {host} on port {port}");

			return new Channel(host, port, credentials,
				CreateChannelOptions(maxMessageLength));
		}

		public static int GetFreeTcpPort()
		{
			var tcpListener = new TcpListener(IPAddress.Loopback, 0);

			tcpListener.Start();

			var port = ((IPEndPoint) tcpListener.LocalEndpoint).Port;

			tcpListener.Stop();

			_logger.LogDebug("Using ephemeral port {port}", port);

			return port;
		}

		/// <summary>
		///     Determines whether the specified endpoint is connected to the specified service
		///     that responds with health status 'Serving' .
		/// </summary>
		/// <param name="healthClient"></param>
		/// <param name="serviceName"></param>
		/// <param name="statusCode">Status code from the RPC call</param>
		/// <returns></returns>
		public static bool IsServing([NotNull] Health.HealthClient healthClient,
		                             [NotNull] string serviceName,
		                             out StatusCode statusCode)
		{
			statusCode = StatusCode.Unknown;

			try
			{
				HealthCheckResponse healthResponse =
					healthClient.Check(new HealthCheckRequest()
						{Service = serviceName});

				statusCode = StatusCode.OK;

				return healthResponse.Status == HealthCheckResponse.Types.ServingStatus.Serving;
			}
			catch (RpcException rpcException)
			{
				_logger.LogDebug(rpcException, "Error checking health of service {serviceName}",
					serviceName);

				statusCode = rpcException.StatusCode;
			}
			catch (Exception e)
			{
				_logger.LogDebug(e, "Error checking health of service {serviceName}", serviceName);

				return false;
			}

			return false;
		}
	}
}
