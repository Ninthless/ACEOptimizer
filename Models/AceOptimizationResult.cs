using System.Collections.Generic;

namespace ACEOptimizer.Models
{
    public sealed record AceOptimizationResult(IReadOnlyCollection<string> DetectedProcesses, bool AccessDenied)
    {
        public bool HasDetectedProcesses => DetectedProcesses.Count > 0;
    }
}
