using System.Windows.Forms;
using JKMon.App.Update;

namespace JKMon.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var arguments = UpdateArguments.Parse(args);
        if (arguments.IsApply)
        {
            // This instance is the staged build swapping files for the installed one; it shows no interface.
            return UpdateApplier.Run(arguments);
        }

        ApplicationConfiguration.Initialize();
        System.Windows.Forms.Application.Run(new JkMonContext(arguments));
        return 0;
    }
}
