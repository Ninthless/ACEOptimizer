using System;
using System.Diagnostics;

namespace ACEOptimizer.Services
{
    internal sealed class AutoStartService
    {
        private const string TaskName = "ACEOptimizer";

        public bool IsEnabled()
        {
            try
            {
                using Process process = CreatePowerShellProcess($"Get-ScheduledTask -TaskName '{TaskName}' -ErrorAction Stop", redirectOutput: true);
                process.Start();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public void Enable(string executablePath)
        {
            string escapedPath = executablePath.Replace("'", "''");
            string command =
                $"$a = New-ScheduledTaskAction -Execute '{escapedPath}'; " +
                $"$t = New-ScheduledTaskTrigger -AtLogon; " +
                $"Register-ScheduledTask -TaskName '{TaskName}' -Action $a -Trigger $t -RunLevel Highest -Force";

            RunPowerShellOrThrow(command);
        }

        public void Disable()
        {
            using Process process = CreatePowerShellProcess(
                $"Unregister-ScheduledTask -TaskName '{TaskName}' -Confirm:$false -ErrorAction SilentlyContinue",
                redirectOutput: false);

            process.Start();
            process.WaitForExit();
        }

        private void RunPowerShellOrThrow(string command)
        {
            using Process process = CreatePowerShellProcess(command, redirectOutput: true);
            process.Start();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
                return;

            throw new InvalidOperationException(standardError);
        }

        private static Process CreatePowerShellProcess(string command, bool redirectOutput)
        {
            return new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NonInteractive -NoProfile -Command \"{command}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = redirectOutput,
                    RedirectStandardError = redirectOutput
                }
            };
        }
    }
}
