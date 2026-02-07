using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tomlyn;
using Tomlyn.Model;

namespace Graft.VS2026Extension.Graft
{
    internal sealed class GraftService : IDisposable
    {
        private readonly string _solutionDir;
        private readonly string? _repoPath;
        private readonly string? _gitCommonDir;
        private readonly string? _graftBinaryPath;
        private readonly FileWatcher _fileWatcher = new FileWatcher();

        public event EventHandler? DataChanged;

        public bool IsAvailable => _graftBinaryPath != null;
        public string? RepoPath => _repoPath;

        public GraftService(string solutionDir)
        {
            _solutionDir = solutionDir;
            _repoPath = ResolveRepoPath(solutionDir);
            _graftBinaryPath = FindGraftBinary();

            if (_repoPath != null)
            {
                _gitCommonDir = ResolveGitCommonDir(_repoPath);
                var graftDir = Path.Combine(_gitCommonDir, "graft");
                _fileWatcher.Changed += (s, e) => DataChanged?.Invoke(this, EventArgs.Empty);
                _fileWatcher.Watch(graftDir);
            }
        }

        // --- Direct file reads (no CLI needed) ---

        public string? GetActiveStackName()
        {
            if (_gitCommonDir == null) return null;

            var path = Path.Combine(_gitCommonDir, "graft", "active-stack");
            if (!File.Exists(path)) return null;

            var name = File.ReadAllText(path, Encoding.UTF8).Trim();
            return string.IsNullOrEmpty(name) ? null : name;
        }

        public List<StackInfo> LoadAllStacks()
        {
            var stacksDir = GetStacksDirectory();
            if (stacksDir == null || !Directory.Exists(stacksDir))
                return new List<StackInfo>();

            var activeStack = GetActiveStackName();
            var stacks = new List<StackInfo>();

            foreach (var file in Directory.GetFiles(stacksDir, "*.toml").OrderBy(f => f))
            {
                var stack = LoadStackFromFile(file);
                if (stack != null)
                {
                    stack.IsActive = string.Equals(stack.Name, activeStack, StringComparison.Ordinal);
                    stacks.Add(stack);
                }
            }

            return stacks;
        }

        public string[] ListStackNames()
        {
            var stacksDir = GetStacksDirectory();
            if (stacksDir == null || !Directory.Exists(stacksDir))
                return Array.Empty<string>();

            return Directory.GetFiles(stacksDir, "*.toml")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(n => n)
                .ToArray();
        }

        private string? GetStacksDirectory()
        {
            if (_gitCommonDir == null) return null;
            return Path.Combine(_gitCommonDir, "graft", "stacks");
        }

        private StackInfo? LoadStackFromFile(string filePath)
        {
            try
            {
                var toml = File.ReadAllText(filePath, Encoding.UTF8);
                var table = Toml.ToModel(toml);

                var stack = new StackInfo
                {
                    Name = table.TryGetValue("name", out var n) && n is string nameStr
                        ? nameStr
                        : Path.GetFileNameWithoutExtension(filePath),
                    Trunk = table.TryGetValue("trunk", out var t) && t is string trunkStr
                        ? trunkStr
                        : "main",
                };

                if (table.TryGetValue("created_at", out var ca))
                {
                    if (ca is string caStr)
                        stack.CreatedAt = DateTime.Parse(caStr, CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind);
                    else if (ca is TomlDateTime dt)
                        stack.CreatedAt = dt.DateTime.DateTime;
                }

                if (table.TryGetValue("branches", out var branchesObj) &&
                    branchesObj is TomlTableArray branches)
                {
                    foreach (TomlTable branchTable in branches)
                    {
                        if (branchTable.TryGetValue("name", out var bn) && bn is string branchName)
                        {
                            var branch = new BranchInfo { Name = branchName };

                            if (branchTable.TryGetValue("pr_number", out var prNum))
                            {
                                try
                                {
                                    branch.PrNumber = Convert.ToUInt64(prNum, CultureInfo.InvariantCulture);
                                }
                                catch (Exception ex) when (
                                    ex is FormatException ||
                                    ex is InvalidCastException ||
                                    ex is OverflowException)
                                {
                                    // Ignore malformed PR number
                                }
                            }

                            if (branchTable.TryGetValue("pr_url", out var prUrl) && prUrl is string prUrlStr)
                                branch.PrUrl = prUrlStr;

                            if (branchTable.TryGetValue("pr_state", out var prState) &&
                                prState is string prStateStr)
                                branch.PrState = prStateStr;

                            stack.Branches.Add(branch);
                        }
                    }
                }

                return stack;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load stack from {filePath}: {ex.Message}");
                return null;
            }
        }

        // --- CLI execution (for write operations) ---

        public async Task<CliResult> RunCommandAsync(string arguments,
            CancellationToken cancellationToken = default)
        {
            if (_graftBinaryPath == null)
                return new CliResult(-1, "", "Graft binary not found. Install graft and try again.");

            var psi = new ProcessStartInfo
            {
                FileName = _graftBinaryPath,
                Arguments = arguments,
                WorkingDirectory = _repoPath ?? _solutionDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using (var process = new Process { StartInfo = psi })
            {
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null) stdout.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null) stderr.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (cancellationToken.Register(() =>
                {
                    try { process.Kill(); }
                    catch { /* already exited */ }
                }))
                {
                    await Task.Run(() => process.WaitForExit(), cancellationToken).ConfigureAwait(false);
                }

                return new CliResult(process.ExitCode, stdout.ToString(), stderr.ToString());
            }
        }

        private static string SanitizeArgument(string value)
        {
            if (value.IndexOfAny(new[] { '"', '\\', '\0', '\n', '\r' }) >= 0)
                throw new ArgumentException($"Argument contains invalid characters: '{value}'");
            return value;
        }

        public async Task<CliResult> InitStackAsync(string name, string? baseBranch = null)
        {
            var args = $"stack init \"{SanitizeArgument(name)}\"";
            if (baseBranch != null)
                args += $" -b \"{SanitizeArgument(baseBranch)}\"";
            return await RunCommandAsync(args).ConfigureAwait(false);
        }

        public async Task<CliResult> PushBranchAsync(string branchName, bool create = false)
        {
            var args = $"stack push \"{SanitizeArgument(branchName)}\"";
            if (create)
                args += " -c";
            return await RunCommandAsync(args).ConfigureAwait(false);
        }

        public async Task<CliResult> PopBranchAsync()
        {
            return await RunCommandAsync("stack pop").ConfigureAwait(false);
        }

        public async Task<CliResult> SyncStackAsync(string? branchName = null)
        {
            var args = "stack sync";
            if (branchName != null)
                args += $" \"{SanitizeArgument(branchName)}\"";
            return await RunCommandAsync(args).ConfigureAwait(false);
        }

        public async Task<CliResult> SwitchStackAsync(string name)
        {
            return await RunCommandAsync($"stack switch \"{SanitizeArgument(name)}\"").ConfigureAwait(false);
        }

        public async Task<CliResult> StackLogAsync()
        {
            return await RunCommandAsync("stack log").ConfigureAwait(false);
        }

        // --- Binary detection ---

        private static string? FindGraftBinary()
        {
            // Check PATH
            var pathBinary = FindInPath("graft") ?? FindInPath("gt");
            if (pathBinary != null)
                return pathBinary;

            // Windows-specific locations
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                var candidate = Path.Combine(localAppData, "Programs", "Graft", "graft.exe");
                if (File.Exists(candidate))
                    return candidate;
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFiles))
            {
                var candidate = Path.Combine(programFiles, "Graft", "graft.exe");
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private static string? FindInPath(string executable)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv == null) return null;

            var extensions = new[] { "", ".exe" };
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir))
                    continue;

                foreach (var ext in extensions)
                {
                    var candidate = Path.Combine(dir, executable + ext);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }

            return null;
        }

        // --- Git directory resolution (mirrors GitRunner logic) ---

        private static string ResolveGitCommonDir(string workingDir)
        {
            var gitPath = Path.Combine(workingDir, ".git");

            if (Directory.Exists(gitPath))
                return gitPath;

            if (File.Exists(gitPath))
            {
                var content = File.ReadAllText(gitPath, Encoding.UTF8).Trim();
                if (content.StartsWith("gitdir:", StringComparison.Ordinal))
                {
                    var gitDir = content.Substring("gitdir:".Length).Trim();
                    if (!Path.IsPathRooted(gitDir))
                        gitDir = Path.GetFullPath(Path.Combine(workingDir, gitDir));

                    var commonDirFile = Path.Combine(gitDir, "commondir");
                    if (File.Exists(commonDirFile))
                    {
                        var commonDir = File.ReadAllText(commonDirFile, Encoding.UTF8).Trim();
                        if (!Path.IsPathRooted(commonDir))
                            commonDir = Path.GetFullPath(Path.Combine(gitDir, commonDir));
                        return commonDir;
                    }

                    return gitDir;
                }
            }

            return gitPath;
        }

        private static string? ResolveRepoPath(string dir)
        {
            var current = dir;
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current, ".git")) ||
                    File.Exists(Path.Combine(current, ".git")))
                    return current;

                current = Path.GetDirectoryName(current);
            }

            return null;
        }

        public void Dispose()
        {
            _fileWatcher.Dispose();
        }
    }
}
