// Copyright © 2024-Present The Cloud Streams Authors
//
// Licensed under the Apache License, Version 2.0 (the "License"),
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CloudStreams.Operator.Services;

/// <summary>
/// Issues the certificates of the admission webhook: a CA of the operator's own, and the serving certificates it signs (see #278)
/// </summary>
public static class CertificateHelper
{

    /// <summary>
    /// Gets how long the CAs the operator issues are valid for
    /// </summary>
    public static readonly TimeSpan CaValidity = TimeSpan.FromDays(3650);

    /// <summary>
    /// Gets how long before it expires a CA is replaced. Until it expires, the CA bundle holds both, so that the certificates it signed remain trusted
    /// </summary>
    public static readonly TimeSpan CaRenewBefore = TimeSpan.FromDays(365);

    /// <summary>
    /// Creates a new self-signed CA
    /// </summary>
    /// <param name="commonName">The common name of the CA</param>
    /// <param name="now">The current date and time</param>
    /// <returns>A new CA, with its private key</returns>
    public static X509Certificate2 CreateCa(string commonName, DateTimeOffset now)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={commonName}", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return request.CreateSelfSigned(now.AddMinutes(-5), now.Add(CaValidity));
    }

    /// <summary>
    /// Creates a new serving certificate for the specified service, signed by the specified CA
    /// </summary>
    /// <param name="ca">The CA to sign the certificate with, with its private key</param>
    /// <param name="service">The name of the service the certificate is issued for</param>
    /// <param name="namespace">The namespace of the service</param>
    /// <param name="now">The current date and time</param>
    /// <param name="validity">How long the certificate is valid for</param>
    /// <returns>A new serving certificate, with its private key</returns>
    public static X509Certificate2 CreateServingCertificate(X509Certificate2 ca, string service, string @namespace, DateTimeOffset now, TimeSpan validity)
    {
        ArgumentNullException.ThrowIfNull(ca);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={service}.{@namespace}.svc", key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();
        foreach (var name in GetServiceNames(service, @namespace)) names.AddDnsName(name);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(ca, true, false));
        var notAfter = now.Add(validity) < ca.NotAfter ? now.Add(validity) : new DateTimeOffset(ca.NotAfter.ToUniversalTime());
        var serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7F;
        using var certificate = request.Create(ca, now.AddMinutes(-5), notAfter, serial);
        return certificate.CopyWithPrivateKey(key);
    }

    /// <summary>
    /// Gets the DNS names the API server may call the specified service by
    /// </summary>
    /// <param name="service">The name of the service</param>
    /// <param name="namespace">The namespace of the service</param>
    /// <returns>The DNS names of the service</returns>
    public static IEnumerable<string> GetServiceNames(string service, string @namespace) =>
    [
        service,
        $"{service}.{@namespace}",
        $"{service}.{@namespace}.svc",
        $"{service}.{@namespace}.svc.cluster.local"
    ];

    /// <summary>
    /// Exports the specified certificate as PEM
    /// </summary>
    /// <param name="certificate">The certificate to export</param>
    /// <returns>The PEM of the certificate</returns>
    public static string ExportCertificate(X509Certificate2 certificate) => certificate.ExportCertificatePem() + "\n";

    /// <summary>
    /// Exports the private key of the specified certificate as PKCS#8 PEM
    /// </summary>
    /// <param name="certificate">The certificate whose private key to export</param>
    /// <returns>The PEM of the private key</returns>
    public static string ExportPrivateKey(X509Certificate2 certificate)
    {
        using var key = certificate.GetECDsaPrivateKey() ?? throw new InvalidOperationException("The certificate has no ECDSA private key");
        return key.ExportPkcs8PrivateKeyPem() + "\n";
    }

    /// <summary>
    /// Loads a certificate and its private key from PEM
    /// </summary>
    /// <param name="certificatePem">The PEM of the certificate</param>
    /// <param name="keyPem">The PEM of the private key</param>
    /// <returns>The loaded certificate</returns>
    public static X509Certificate2 Load(string certificatePem, string keyPem)
    {
        using var certificate = X509Certificate2.CreateFromPem(certificatePem, keyPem);
        // SslStream requires a key that isn't ephemeral on some platforms
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), null);
    }

    /// <summary>
    /// Reads the certificates of a PEM bundle
    /// </summary>
    /// <param name="bundle">The PEM bundle to read</param>
    /// <returns>The certificates of the bundle</returns>
    public static List<X509Certificate2> ReadBundle(string? bundle)
    {
        var certificates = new X509Certificate2Collection();
        if (!string.IsNullOrWhiteSpace(bundle)) certificates.ImportFromPem(bundle);
        return [.. certificates];
    }

    /// <summary>
    /// Determines whether the specified certificate must be renewed
    /// </summary>
    /// <param name="certificate">The certificate to check</param>
    /// <param name="now">The current date and time</param>
    /// <param name="renewBefore">How long before it expires a certificate is renewed</param>
    /// <returns>A boolean indicating whether the certificate must be renewed</returns>
    public static bool MustRenew(X509Certificate2 certificate, DateTimeOffset now, TimeSpan renewBefore) => new DateTimeOffset(certificate.NotAfter.ToUniversalTime()) - now <= renewBefore;

    /// <summary>
    /// Gets the bytes of the specified text, as UTF-8
    /// </summary>
    /// <param name="text">The text to encode</param>
    /// <returns>The UTF-8 bytes of the text</returns>
    public static byte[] ToBytes(string text) => Encoding.UTF8.GetBytes(text);

}
