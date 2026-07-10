using ACEOptimizer.Services;

namespace ACEOptimizer.Tests;

public class AuthenticodeVerifierTests
{
    [Fact]
    public void IsTrusted_WithUnsignedFile_ReturnsFalse()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"ACEOptimizerUnsigned_{Guid.NewGuid():N}.exe");
        File.WriteAllText(filePath, "unsigned");

        try
        {
            Assert.False(AuthenticodeVerifier.IsTrusted(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void IsTrusted_WithAuthenticodeSignedBinary_ReturnsTrue()
    {
        string dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT")
            ?? throw new InvalidOperationException("DOTNET_ROOT is not configured.");
        string dotnetPath = Path.Combine(dotnetRoot, "dotnet.exe");

        Assert.True(AuthenticodeVerifier.IsTrusted(dotnetPath));
    }
}
