#nullable enable

using System.Text;

namespace RalfHuesing.Mcp.Observability.Tests.AiNetLinter;

public sealed class AiNetLinterTests
{
    private const string LinterExePath = @"C:\Daten\AiNetLinter-win-x64\AiNetLinter.exe";

    [Fact]
    public async Task RunLinterShouldBeClean()
    {
        if (!File.Exists(LinterExePath))
        {
            Assert.Skip("AiNetLinter.exe was not found at path: " + LinterExePath);
            return;
        }

        string solutionRoot = FindSolutionRoot();

        string configPath = Path.Combine(
            solutionRoot, "tests", "RalfHuesing.Mcp.Observability.Tests",
            "AiNetLinter", "rules", "RalfHuesing.Mcp.Observability.rules.json");

        string outputReportDir = Path.Combine(
            solutionRoot, "tests", "RalfHuesing.Mcp.Observability.Tests",
            "AiNetLinter", "output");

        string outputReportFile = Path.Combine(outputReportDir, "linter-report.md");

        string targetRulesFile = Path.Combine(solutionRoot, ".agents", "rules", "AiNetLinter.mdc");

        Directory.CreateDirectory(outputReportDir);

        // Step 1: Validate — no baseline, full clean check
        var validationArgs = new[]
        {
            "--config", $"\"{configPath}\"",
            "--path", $"\"{solutionRoot}\""
        };

        var (valExitCode, valStdout, valStderr) = await RunLinterProcessAsync(
            string.Join(" ", validationArgs), solutionRoot, TestContext.Current.CancellationToken);

        var reportContent = new StringBuilder();
        reportContent.AppendLine("# AiNetLinter Run Report");
        reportContent.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
            $"- **Timestamp:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        reportContent.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
            $"- **Validation Exit Code:** {valExitCode}");
        reportContent.AppendLine();
        reportContent.AppendLine("## Validation Output");
        reportContent.AppendLine("```");
        reportContent.AppendLine(valStdout);
        reportContent.AppendLine("```");

        if (!string.IsNullOrWhiteSpace(valStderr))
        {
            reportContent.AppendLine();
            reportContent.AppendLine("## Validation Errors");
            reportContent.AppendLine("```");
            reportContent.AppendLine(valStderr);
            reportContent.AppendLine("```");
        }

        await File.WriteAllTextAsync(
            outputReportFile, reportContent.ToString(), Encoding.UTF8,
            TestContext.Current.CancellationToken);

        if (valExitCode != 0)
        {
            Assert.Fail(
                $"AiNetLinter validation failed (exit {valExitCode}). " +
                $"See report: {outputReportFile}\r\n{valStderr}\r\n{valStdout}");
        }

        // Step 2: Sync agent rules (only on clean validation)
        Directory.CreateDirectory(Path.GetDirectoryName(targetRulesFile)!);

        var syncArgs = new[]
        {
            "--config", $"\"{configPath}\"",
            "--path", $"\"{solutionRoot}\"",
            "--sync-agent-rules",
            "--agent-rules-path", $"\"{targetRulesFile}\""
        };

        var (syncExitCode, syncStdout, syncStderr) = await RunLinterProcessAsync(
            string.Join(" ", syncArgs), solutionRoot, TestContext.Current.CancellationToken);

        if (syncExitCode != 0)
        {
            Assert.Fail(
                $"AiNetLinter rule sync failed (exit {syncExitCode}).\r\n{syncStderr}\r\n{syncStdout}");
        }

        Assert.True(
            File.Exists(targetRulesFile),
            $"Rules file not found after sync: {targetRulesFile}");
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunLinterProcessAsync(
        string argumentsString, string solutionRoot, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = LinterExePath,
            Arguments = argumentsString,
            WorkingDirectory = solutionRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdoutBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderrBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode, stdoutBuilder.ToString(), stderrBuilder.ToString());
    }

    private static string FindSolutionRoot()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null)
        {
            if (currentDir.GetFiles("RalfHuesing.Mcp.Observability.sln").Length > 0)
            {
                return currentDir.FullName;
            }

            currentDir = currentDir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Solution root folder with RalfHuesing.Mcp.Observability.sln not found.");
    }
}
