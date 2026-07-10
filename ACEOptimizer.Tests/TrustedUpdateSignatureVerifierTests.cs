using System.Text;
using ACEOptimizer.Services;
using NetSparkleUpdater.Enums;

namespace ACEOptimizer.Tests;

public class TrustedUpdateSignatureVerifierTests
{
    private static readonly byte[] SignedData = Encoding.UTF8.GetBytes("ACEOptimizer update signature test");

    [Theory]
    [InlineData("WJPXzic0vcJ/Xzq4vnAIsii7D3/hPzhaXS/pKVDr2QQhXqRvLegCN3qmBmtAG86cnukdoPc1FZAZ5TtJTuOaBw==")]
    [InlineData("xuM9sXlZpaHc+YC4G31Ta/toxj2sN10YUwuIsZp6KZkw7sqmNIM2EdiLAVsj1PQUJdRrMqiYNsJHZrw9IkOuBQ==")]
    public void VerifySignature_WithCurrentOrNextKey_ReturnsValid(string signature)
    {
        TrustedUpdateSignatureVerifier verifier = new(UpdateSecurity.TrustedPublicKeys);

        ValidationResult result = verifier.VerifySignature(signature, SignedData);

        Assert.Equal(ValidationResult.Valid, result);
    }

    [Fact]
    public void VerifySignature_WithAlteredData_ReturnsInvalid()
    {
        TrustedUpdateSignatureVerifier verifier = new(UpdateSecurity.TrustedPublicKeys);

        ValidationResult result = verifier.VerifySignature(
            "WJPXzic0vcJ/Xzq4vnAIsii7D3/hPzhaXS/pKVDr2QQhXqRvLegCN3qmBmtAG86cnukdoPc1FZAZ5TtJTuOaBw==",
            Encoding.UTF8.GetBytes("altered"));

        Assert.Equal(ValidationResult.Invalid, result);
    }
}
