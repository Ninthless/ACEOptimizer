using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using ACEOptimizer.Models;

namespace ACEOptimizer.Services
{
    internal sealed class AceProcessService
    {
        private static readonly string[] DefaultAceProcesses = ["SGuard64", "SGuardSvc64"];

        public IReadOnlyList<string> AceProcessNames { get; } = DefaultAceProcesses;

        public nint CalculateAffinityMask()
        {
            try
            {
                int coreCount = Math.Max(Environment.ProcessorCount, 1);
                return (nint)(1L << (coreCount - 1));
            }
            catch
            {
                return 1;
            }
        }

        public AceOptimizationResult EvaluateAndOptimize(nint affinityMask)
        {
            HashSet<string> detectedProcesses = new(StringComparer.OrdinalIgnoreCase);
            bool accessDenied = false;

            foreach (string aceProcessName in AceProcessNames)
            {
                Process[] processes;
                try
                {
                    processes = Process.GetProcessesByName(aceProcessName);
                }
                catch
                {
                    continue;
                }

                foreach (Process process in processes)
                {
                    using (process)
                    {
                        detectedProcesses.Add(aceProcessName);
                        TryOptimizeProcess(process, affinityMask, ref accessDenied);
                    }
                }
            }

            return new AceOptimizationResult(detectedProcesses.OrderBy(name => name).ToArray(), accessDenied);
        }

        private void TryOptimizeProcess(Process process, nint affinityMask, ref bool accessDenied)
        {
            TrySetPriorityToIdle(process, ref accessDenied);
            TrySetAffinityToLastCore(process, affinityMask, ref accessDenied);
        }

        private void TrySetPriorityToIdle(Process process, ref bool accessDenied)
        {
            try
            {
                if (process.PriorityClass == ProcessPriorityClass.Idle)
                    return;

                process.PriorityClass = ProcessPriorityClass.Idle;
            }
            catch (Win32Exception ex) when (IsAccessDenied(ex))
            {
                accessDenied = true;
            }
            catch (UnauthorizedAccessException)
            {
                accessDenied = true;
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void TrySetAffinityToLastCore(Process process, nint affinityMask, ref bool accessDenied)
        {
            try
            {
                if (process.ProcessorAffinity == affinityMask)
                    return;

                process.ProcessorAffinity = affinityMask;
            }
            catch (Win32Exception ex) when (IsAccessDenied(ex))
            {
                accessDenied = true;
            }
            catch (UnauthorizedAccessException)
            {
                accessDenied = true;
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static bool IsAccessDenied(Win32Exception exception)
        {
            return exception.NativeErrorCode == 5
                || exception.Message.Contains("Access is denied", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("拒绝访问", StringComparison.OrdinalIgnoreCase);
        }
    }
}
