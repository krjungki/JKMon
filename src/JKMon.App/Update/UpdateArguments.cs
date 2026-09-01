namespace JKMon.App.Update;

/// <summary>
/// The switches the app answers to. The staged copy of the new build runs itself with `--apply-update` from the
/// temporary folder, because a running executable cannot replace its own file.
/// </summary>
internal sealed record UpdateArguments
{
    internal const string ApplySwitch = "--apply-update";
    internal const string CleanupSwitch = "--cleanup";

    internal string? SourceDirectory { get; init; }

    internal string? TargetDirectory { get; init; }

    /// <summary>The staging root the relaunched app should delete once it is running from the new files.</summary>
    internal string? WorkDirectory { get; init; }

    internal int WaitForProcessId { get; init; }

    internal string? CleanupDirectory { get; init; }

    internal bool IsApply => SourceDirectory is { Length: > 0 } && TargetDirectory is { Length: > 0 };

    /// <summary>Unknown arguments are ignored so a stray shortcut cannot stop the app from starting.</summary>
    internal static UpdateArguments Parse(string[] args)
    {
        string? source = null;
        string? target = null;
        string? work = null;
        string? cleanup = null;
        var pid = 0;
        var apply = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case ApplySwitch:
                    apply = true;
                    break;

                case "--source" when i + 1 < args.Length:
                    source = args[++i];
                    break;

                case "--target" when i + 1 < args.Length:
                    target = args[++i];
                    break;

                case "--work" when i + 1 < args.Length:
                    work = args[++i];
                    break;

                case "--pid" when i + 1 < args.Length:
                    _ = int.TryParse(args[++i], out pid);
                    break;

                case CleanupSwitch when i + 1 < args.Length:
                    cleanup = args[++i];
                    break;
            }
        }

        return new UpdateArguments
        {
            SourceDirectory = apply ? source : null,
            TargetDirectory = apply ? target : null,
            WorkDirectory = work,
            WaitForProcessId = pid,
            CleanupDirectory = cleanup
        };
    }
}
