namespace Graft.VS2026Extension.Graft
{
    internal sealed class CliResult
    {
        public int ExitCode { get; }
        public string Stdout { get; }
        public string Stderr { get; }
        public bool Success => ExitCode == 0;

        public CliResult(int exitCode, string stdout, string stderr)
        {
            ExitCode = exitCode;
            Stdout = stdout;
            Stderr = stderr;
        }

        public override string ToString()
        {
            if (Success)
                return Stdout;
            return $"Exit code {ExitCode}: {Stderr}";
        }
    }
}
