using System.Collections.Generic;

namespace ACEOptimizer.Models
{
    internal sealed record AceOptimizationResult(IReadOnlyCollection<string> DetectedProcesses, bool AccessDenied)
    {
        public bool HasDetectedProcesses => DetectedProcesses.Count > 0;
    }
}
