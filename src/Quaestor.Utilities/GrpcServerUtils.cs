using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Grpc.Core;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Quaestor.Environment;

namespace Quaestor.Utilities
{
	public static class GrpcServerUtils
	{
		private static readonly ILogger _logger = Log.CreateLogger(nameof(GrpcServerUtils));

		public static void GracefullyStop(Server server)
		{
			if (server == null)
			{
				throw new ArgumentNullException(nameof(server));
			}

			_logger.LogInformation("Starting shut down...");

			server.ShutdownAsync().Wait();

			_logger.LogInformation("Server shut down.");
		}

		/// <summary>
		///     Creates the server credentials using either two PEM files or a certificate from the
		///     Certificate Store.
		/// </summary>
		/// <param name="certificate">
		///     The certificate store's certificate (subject or thumbprint)
		///     or the PEM file containing the certificate chain.
		/// </param>
		/// <param name="privateKeyFilePath">
		///     The PEM file containing the private key (only if the
		///     certificate was provided by a PEM file
		/// </param>
		/// <param name="enforceMutualTls">Enforce client authentication.</param>
		/// <returns></returns>
		public static ServerCredentials GetServerCredentials(
			[CanBeNull] string certificate,
			[CanBeNull] string privateKeyFilePath,
			bool enforceMutualTls = false)
		{
			if (string.IsNullOrEmpty(certificate))
			{
				_logger.LogInformation("Certificate was not provided. Using insecure credentials.");

				return ServerCredentials.Insecure;
			}

			KeyPair certificateKeyPair =
				TryGetServerCertificateKeyPair(certificate, privateKeyFilePath);

			if (certificateKeyPair == null)
			{
				return ServerCredentials.Insecure;
			}

			List<KeyCertificatePair> keyCertificatePairs =
				new List<KeyCertificatePair>
				{
					new KeyCertificatePair(
						certificateKeyPair.PublicKey, certificateKeyPair.PrivateKey)
				};

			string rootCertificatesAsPem =
				CertificateUtils.GetUserRootCertificatesInPemFormat();

			// If not required, still verify the client certificate, if presented
			var clientCertificates =
				enforceMutualTls
					? SslClientCertificateRequestType.RequestAndRequireAndVerify
					: SslClientCertificateRequestType.RequestAndVerify;

			ServerCredentials result = new SslServerCredentials(
				keyCertificatePairs, rootCertificatesAsPem,
				clientCertificates);

			return result;
		}

		public static IList<ChannelOption> CreateChannelOptions(int maxMessageLength)
		{
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

		private static KeyPair TryGetServerCertificateKeyPair(
			[NotNull] string certificate,
			[CanBeNull] string privateKeyFilePath)
		{
			KeyPair result;
			if (File.Exists(certificate))
			{
				_logger.LogDebug("Using existing PEM file certificate: {cert}.", certificate);

				if (string.IsNullOrEmpty(privateKeyFilePath))
				{
					throw new ArgumentException("Private key PEM file was not provided.");
				}

				if (!File.Exists(privateKeyFilePath))
				{
					throw new ArgumentException(
						$"Private key PEM file {privateKeyFilePath} was not found.");
				}

				result = new KeyPair(File.ReadAllText(certificate),
					File.ReadAllText(privateKeyFilePath));

				_logger.LogInformation("Using certificate from file {cert}", certificate);
			}
			else
			{
				_logger.LogDebug(
					"No certificate PEM file found using {cert}. Getting certificate from store.",
					certificate);

				if (!string.IsNullOrEmpty(privateKeyFilePath))
				{
					result = GetMixedKeyPair(certificate, privateKeyFilePath);
				}
				else
				{
					// Find server certificate including private key from Store (Local Computer, Personal folder)
					result =
						CertificateUtils.FindKeyCertificatePairFromStore(
							certificate,
							new[]
							{
								X509FindType.FindBySubjectDistinguishedName,
								X509FindType.FindByThumbprint
							}, StoreName.My, StoreLocation.LocalMachine);
				}

				if (result == null)
				{
					_logger.LogInformation(
						"No certificate could be found by '{cert}'. Using insecure credentials (no TLS).",
						certificate);
				}
				else
				{
					_logger.LogInformation("Using certificate from certificate store for TLS.");
				}
			}

			return result;
		}

		/// <summary>
		///     Gets the public certificate from the certificate store and the private key from the
		///     specified file.
		/// </summary>
		/// <param name="certificate"></param>
		/// <param name="privateKeyFilePath"></param>
		/// <returns></returns>
		private static KeyPair GetMixedKeyPair(string certificate, string privateKeyFilePath)
		{
			if (!File.Exists(privateKeyFilePath))
			{
				throw new ArgumentException(
					$"Private key PEM file {privateKeyFilePath} was not found. " +
					"In order to use the private key from the certificate, the private key file must not be specified.");
			}

			KeyPair result = null;

			// The private key has been provided already, no need to try to extract it from the store:
			X509Certificate2 x509Certificate2 =
				CertificateUtils.FindValidCertificates(
						StoreName.My, StoreLocation.LocalMachine,
						certificate, new[]
						{
							X509FindType
								.FindBySubjectDistinguishedName,
							X509FindType.FindByThumbprint
						})
					.FirstOrDefault();

			if (x509Certificate2 != null)
			{
				string publicKey = CertificateUtils.ExportToPem(x509Certificate2, true);
				string privateKey = File.ReadAllText(privateKeyFilePath);

				result = new KeyPair(privateKey, publicKey);
			}
			else
			{
				_logger.LogInformation("Certificate not found in certificate store.");
			}

			return result;
		}

		private static void ThrowExceptionToClient(Exception exception)
		{
			// This is to avoid a generic exception with little meaning

			// Determine if it is a good idea to use metadata trailers:

			//serverCallContext.ResponseTrailers.Add("ERROR", exception.Message);

			//// This causes a different statuts code / message(probably too long / or illegal characters!)
			//serverCallContext.ResponseTrailers.Add("EXCEPTION",
			//                                       exception.ToString());

			// TODO: Add exception type, error code, etc.

			// TODO: Check if this is still the case:
			// For synchronous calls, there is no result object to extract the trailers from. Simply use the exception

			var rpcException =
				new RpcException(new Status(StatusCode.Unavailable, exception.ToString()),
					exception.Message);

			_logger.LogDebug(exception, "Re-throwing exception as RPC Exception");

			throw rpcException;
		}
	}
}
