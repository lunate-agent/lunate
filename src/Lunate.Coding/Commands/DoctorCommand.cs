namespace Lunate.Coding;

/// <summary>
/// The <c>lunate doctor [--json]</c> subcommand: one offline report of the facts a bug report
/// starts with. It reads the state/config paths, the settings and provider stores, the model
/// catalog, the cached update answer, the terminal capabilities and whether a container engine is
/// on <c>PATH</c>; it never makes a network request, starts no engine and never prints a secret
/// value. Health is the required paths plus a readable settings file: 0 healthy, 1 unhealthy,
/// 2 usage.
/// </summary>
internal static class DoctorCommand
{
    internal const int ExitOk = 0;
    internal const int ExitFailure = 1;
    internal const int ExitUsage = 2;

    internal const string Usage = "Usage: lunate doctor [--json]";

    internal static int Run(
        string[] args,
        TextWriter output,
        TextWriter errors,
        DoctorSeams? seams = null
    )
    {
        if (!TryParse(args, errors, out bool json))
        {
            return ExitUsage;
        }

        try
        {
            DoctorReport report = DoctorReport.Collect(seams ?? new DoctorSeams());
            if (json)
            {
                DoctorView.WriteJson(report, output);
            }
            else
            {
                DoctorView.WriteText(report, output);
            }

            return report.Healthy ? ExitOk : ExitFailure;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            errors.WriteLine($"lunate: {exception.Message}");
            return ExitFailure;
        }
    }

    private static bool TryParse(string[] args, TextWriter errors, out bool json)
    {
        json = false;
        foreach (string arg in args)
        {
            switch (arg)
            {
                case "--json" when !json:
                    json = true;
                    break;
                default:
                    errors.WriteLine(Usage);
                    return false;
            }
        }

        return true;
    }
}
