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

using CloudStreams.Operator.Services;
using System.Security.Cryptography.X509Certificates;

namespace CloudStreams.UnitTests.Cases.Operator;

/// <summary>
/// Covers the certificates the operator issues for its admission webhook (see #278)
/// </summary>
public class CertificateHelperTests
{

    static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void The_Serving_Certificate_Should_Be_Trusted_By_The_Ca_For_The_Names_Of_The_Service()
    {
        // arrange
        using var ca = CertificateHelper.CreateCa("cs-operator-ca", Now);

        // act
        using var serving = CertificateHelper.CreateServingCertificate(ca, "cs-operator", "cloud-streams", Now, TimeSpan.FromDays(365));

        // assert
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        Assert.True(chain.Build(serving));
        Assert.True(serving.MatchesHostname("cs-operator.cloud-streams.svc"));
        Assert.True(serving.MatchesHostname("cs-operator.cloud-streams.svc.cluster.local"));
        Assert.False(serving.MatchesHostname("other.cloud-streams.svc"));
    }

    [Fact]
    public void Certificates_Should_Round_Trip_Through_Pem()
    {
        using var ca = CertificateHelper.CreateCa("ca", Now);
        using var serving = CertificateHelper.CreateServingCertificate(ca, "svc", "ns", Now, TimeSpan.FromDays(30));
        using var loaded = CertificateHelper.Load(CertificateHelper.ExportCertificate(serving), CertificateHelper.ExportPrivateKey(serving));
        Assert.Equal(serving.Thumbprint, loaded.Thumbprint);
        Assert.True(loaded.HasPrivateKey);
        var bundle = CertificateHelper.ReadBundle(CertificateHelper.ExportCertificate(ca) + CertificateHelper.ExportCertificate(serving));
        Assert.Equal([ca.Thumbprint, serving.Thumbprint], bundle.Select(c => c.Thumbprint));
    }

    [Fact]
    public void A_Certificate_Should_Be_Renewed_Before_It_Expires()
    {
        using var ca = CertificateHelper.CreateCa("ca", Now);
        using var serving = CertificateHelper.CreateServingCertificate(ca, "svc", "ns", Now, TimeSpan.FromDays(365));
        Assert.False(CertificateHelper.MustRenew(serving, Now, TimeSpan.FromDays(30)));
        Assert.True(CertificateHelper.MustRenew(serving, Now.AddDays(340), TimeSpan.FromDays(30)));
    }

    [Fact]
    public void A_Serving_Certificate_Should_Not_Outlive_Its_Ca()
    {
        using var ca = CertificateHelper.CreateCa("ca", Now);
        using var serving = CertificateHelper.CreateServingCertificate(ca, "svc", "ns", Now.AddDays(3600), TimeSpan.FromDays(365));
        Assert.True(serving.NotAfter <= ca.NotAfter);
    }

}
