using System.Runtime.Versioning;
using JKMon.Core.Interop;

namespace JKMon.Core.Metrics;

/// <summary>
/// Per-logical-processor busy percentages from PDH. English counter paths keep this working on a localized Windows,
/// matching how the disk counters are read.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class CpuCoreCounter : IDisposable
{
    private const uint PdhSuccess = 0;

    private readonly List<IntPtr> _counters = [];

    private IntPtr _query = IntPtr.Zero;
    private bool _primed;

    internal CpuCoreCounter()
    {
        try
        {
            if (NativeMethods.PdhOpenQueryW(IntPtr.Zero, IntPtr.Zero, out _query) != PdhSuccess)
            {
                return;
            }

            // Processor instances above the first group cannot be addressed this way, so stop at the first gap.
            for (var core = 0; core < Environment.ProcessorCount; core++)
            {
                var path = $@"\Processor({core})\% Processor Time";
                if (NativeMethods.PdhAddEnglishCounterW(_query, path, IntPtr.Zero, out var counter) != PdhSuccess)
                {
                    break;
                }

                _counters.Add(counter);
            }
        }
        catch (DllNotFoundException)
        {
            _counters.Clear();
        }
        catch (EntryPointNotFoundException)
        {
            _counters.Clear();
        }
    }

    internal IReadOnlyList<double> Read()
    {
        if (_counters.Count == 0 || NativeMethods.PdhCollectQueryData(_query) != PdhSuccess)
        {
            return [];
        }

        // Rate counters need two collections before the first formatted value is meaningful.
        if (!_primed)
        {
            _primed = true;
            return [];
        }

        var values = new double[_counters.Count];
        for (var i = 0; i < _counters.Count; i++)
        {
            values[i] = ReadCounter(_counters[i]);
        }

        return values;
    }

    private static double ReadCounter(IntPtr counter)
    {
        if (NativeMethods.PdhGetFormattedCounterValue(counter, NativeMethods.PdhFmtDouble, IntPtr.Zero, out var value)
            != PdhSuccess)
        {
            return 0;
        }

        return double.IsFinite(value.DoubleValue) ? Math.Clamp(value.DoubleValue, 0, 100) : 0;
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero)
        {
            NativeMethods.PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }

        _counters.Clear();
    }
}
