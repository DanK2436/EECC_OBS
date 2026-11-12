using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EeccStudio.Core.Pairing;

public static class StudioCertificate
{
    public static X509Certificate2 Create()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=eecc-studio-lan", ecdsa, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("eecc-studio.lan");
        request.CertificateExtensions.Add(san.Build());

        var issued = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(2));
        var pfx = issued.Export(X509ContentType.Pfx);
        return new X509Certificate2(pfx, (string?)null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
    }

    public static string Fingerprint(X509Certificate2 certificate)
    {
        var hash = SHA256.HashData(certificate.RawData);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
