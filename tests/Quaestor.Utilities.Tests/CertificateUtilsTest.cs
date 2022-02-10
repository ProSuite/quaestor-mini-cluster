using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NUnit.Framework;

namespace Quaestor.Utilities.Tests
{
	public class CertificateUtilsTest
	{
		[Test]
		public void CanGetRootCertificates()
		{
			var rootCertificates = CertificateUtils.GetUserRootCertificates().ToList();

			Assert.IsTrue(rootCertificates.Count > 0);

			foreach (X509Certificate2 certificate in rootCertificates)
			{
				Console.WriteLine(certificate);
			}
		}

		[Test]
		public void CanFindCertificateByCommonName()
		{
			var found = CertificateUtils.FindCertificates(
					StoreName.Root, StoreLocation.CurrentUser,
					"CN=Microsoft Root Certificate Authority, DC=microsoft, DC=com",
					X509FindType.FindBySubjectDistinguishedName, false)
				.ToList();

			Assert.IsTrue(found.Count > 0);
		}

		[Test]
		public void CanFindCertificateByThumbprint()
		{
			var certificate = CertificateUtils.GetCertificates(StoreName.My).First();

			Assert.NotNull(certificate.Thumbprint);

			string certificateThumbprint = certificate.Thumbprint;

			var found =
				CertificateUtils.FindCertificates(
					StoreName.My, StoreLocation.CurrentUser, certificateThumbprint,
					X509FindType.FindByThumbprint, false).ToList();

			Assert.AreEqual(1, found.Count);

			Assert.AreEqual(certificate, found.First());
		}

		[Test]
		public void CanGetMyCertificates()
		{
			var found = CertificateUtils.GetCertificates(StoreName.My).ToList();

			Assert.IsTrue(found.Count > 0);

			foreach (X509Certificate2 certificate in found)
			{
				Console.WriteLine(certificate);
			}
		}

		[Test]
		public void CanGetPrivateKeyFromCertificate()
		{
			// Create the certificate from the pem files
			X509Certificate2 x509 = X509Certificate2.CreateFromPemFile("cert.pem", "key.pem");

			Assert.IsTrue(CertificateUtils.TryExportPrivateKey(x509, out string privateKey, out _));

			Assert.IsNotNull(privateKey);

			// Assuming .net 6 gets it right:
			string expected = ExportPrivateKeyPkcs1Pem(x509);

			Assert.AreEqual(expected, privateKey);
		}

		/// <summary>
		///     Exports the private key of the specified certificate using the new .net 6 method
		/// </summary>
		/// <param name="certificate"></param>
		/// <returns></returns>
		private static string ExportPrivateKeyPkcs1Pem(X509Certificate2 certificate)
		{
			RSA rsa = certificate.GetRSAPrivateKey();
			Assert.NotNull(rsa);

			byte[] privateKeyBytes = rsa.ExportRSAPrivateKey();

			char[] privateKeyPem = PemEncoding.Write("RSA PRIVATE KEY", privateKeyBytes);

			return new string(privateKeyPem);
		}
	}
}
