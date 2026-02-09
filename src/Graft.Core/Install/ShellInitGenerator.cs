namespace Graft.Core.Install;

public static class ShellInitGenerator
{
    public static string GenerateBash() => GeneratePosix();

    public static string GenerateZsh() => GeneratePosix();

    public static string GenerateFish() =>
        """
        function graft
            if test (count $argv) -ge 1; and test "$argv[1]" = "cd"
                set -l rest $argv[2..-1]
                set -l dir (command graft cd $rest)
                set -l rc $status
                if test $rc -eq 0; and test -n "$dir"
                    builtin cd "$dir"; or return
                end
                return $rc
            end
            command graft $argv
        end

        function gt
            if test (count $argv) -ge 1; and test "$argv[1]" = "cd"
                set -l rest $argv[2..-1]
                set -l dir (command gt cd $rest)
                set -l rc $status
                if test $rc -eq 0; and test -n "$dir"
                    builtin cd "$dir"; or return
                end
                return $rc
            end
            command gt $argv
        end
        """;

    public static string GeneratePowershell() =>
        """
        function Invoke-Graft {
            if ($args.Count -ge 1 -and $args[0] -eq 'cd') {
                $rest = $args[1..($args.Count - 1)]
                $dir = & (Get-Command graft -CommandType Application | Select-Object -First 1) cd @rest
                $rc = $LASTEXITCODE
                if ($rc -eq 0 -and $dir) {
                    Set-Location $dir
                }
                return
            }
            & (Get-Command graft -CommandType Application | Select-Object -First 1) @args
        }

        function Invoke-Gt {
            if ($args.Count -ge 1 -and $args[0] -eq 'cd') {
                $rest = $args[1..($args.Count - 1)]
                $dir = & (Get-Command gt -CommandType Application | Select-Object -First 1) cd @rest
                $rc = $LASTEXITCODE
                if ($rc -eq 0 -and $dir) {
                    Set-Location $dir
                }
                return
            }
            & (Get-Command gt -CommandType Application | Select-Object -First 1) @args
        }

        Set-Alias -Name graft -Value Invoke-Graft -Scope Global -Force
        Set-Alias -Name gt -Value Invoke-Gt -Scope Global -Force
        """;

    public static string? Generate(string shell) =>
        shell.ToLowerInvariant() switch
        {
            "bash" => GenerateBash(),
            "zsh" => GenerateZsh(),
            "fish" => GenerateFish(),
            "powershell" or "pwsh" => GeneratePowershell(),
            _ => null,
        };

    public static IReadOnlyList<string> SupportedShells => ["bash", "zsh", "fish", "powershell", "pwsh"];

    private static string GeneratePosix() =>
        """
        graft() {
          if [ "$1" = "cd" ]; then
            shift
            local dir
            dir="$(command graft cd "$@")"
            local rc=$?
            if [ $rc -eq 0 ] && [ -n "$dir" ]; then
              builtin cd "$dir" || return
            fi
            return $rc
          fi
          command graft "$@"
        }

        gt() {
          if [ "$1" = "cd" ]; then
            shift
            local dir
            dir="$(command gt cd "$@")"
            local rc=$?
            if [ $rc -eq 0 ] && [ -n "$dir" ]; then
              builtin cd "$dir" || return
            fi
            return $rc
          fi
          command gt "$@"
        }
        """;
}
