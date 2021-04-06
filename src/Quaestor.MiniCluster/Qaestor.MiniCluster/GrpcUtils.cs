using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;

namespace Quaestor.MiniCluster
{
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
	}
}
