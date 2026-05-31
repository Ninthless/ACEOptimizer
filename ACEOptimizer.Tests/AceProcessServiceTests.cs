using ACEOptimizer.Services;

namespace ACEOptimizer.Tests;

public class AceProcessServiceTests
{
    private readonly AceProcessService _svc = new();

    [Fact]
    public void CalculateAffinityMask_SingleCore_ReturnsOne()
    {
        // 单核：1 << 0 = 1
        // 无法控制 Environment.ProcessorCount，但可以验证返回值是 2^(N-1)
        nint mask = _svc.CalculateAffinityMask();
        int coreCount = Environment.ProcessorCount;
        nint expected = (nint)(1L << (coreCount - 1));
        Assert.Equal(expected, mask);
    }

    [Fact]
    public void CalculateAffinityMask_ReturnsPositiveValue()
    {
        nint mask = _svc.CalculateAffinityMask();
        Assert.True(mask > 0);
    }

    [Fact]
    public void CalculateAffinityMask_IsExactlyOneBitSet()
    {
        nint mask = _svc.CalculateAffinityMask();
        long v = (long)mask;
        // 恰好一个 bit 为 1：v & (v-1) == 0
        Assert.True(v > 0 && (v & (v - 1)) == 0);
    }

    [Fact]
    public void AceProcessNames_ContainsBothExpectedNames()
    {
        Assert.Contains("SGuard64", _svc.AceProcessNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("SGuardSvc64", _svc.AceProcessNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void AceProcessNames_HasExactlyTwoEntries()
    {
        Assert.Equal(2, _svc.AceProcessNames.Count);
    }
}