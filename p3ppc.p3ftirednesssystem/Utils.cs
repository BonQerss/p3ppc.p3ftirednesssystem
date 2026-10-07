using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using System.Diagnostics;
using System.Drawing;

namespace p3ppc.p3ftirednesssystem;

internal static class Utils
{
    private const string Prefix = "[P3F Tiredness]";

    private static ILogger _logger = null!;
    private static Config _config = null!;
    private static IStartupScanner _scanner = null!;

    internal static nint BaseAddress { get; private set; }

    internal static bool Initialise(ILogger logger, Config config, IModLoader modLoader)
    {
        _logger = logger;
        _config = config;

        using var process = Process.GetCurrentProcess();
        BaseAddress = process.MainModule?.BaseAddress ?? 0;
        if (BaseAddress == 0)
        {
            LogError("Unable to resolve the main module base address.");
            return false;
        }

        var controller = modLoader.GetController<IStartupScanner>();
        if (controller == null || !controller.TryGetTarget(out var scanner) || scanner == null)
        {
            LogError("Unable to get Reloaded SigScan controller.");
            return false;
        }

        _scanner = scanner;
        return true;
    }

    internal static void UpdateConfig(Config config) => _config = config;

    internal static void Log(string message)
        => _logger.WriteLine($"{Prefix} {message}");

    internal static void LogDebug(string message)
    {
        if (_config.DebugEnabled)
            _logger.WriteLine($"{Prefix} {message}");
    }

    internal static void LogError(string message)
        => _logger.WriteLine($"{Prefix} ERROR: {message}", Color.Red);

    internal static void LogError(string message, Exception exception)
        => _logger.WriteLine($"{Prefix} ERROR: {message}\n{exception}", Color.Red);

    internal static void SigScan(string name, string pattern, Action<nint> action)
    {
        _scanner.AddMainModuleScan(pattern, result =>
        {
            if (!result.Found)
            {
                LogError($"Unable to find {name}; the tiredness restoration will not install.");
                return;
            }

            nint address = result.Offset + BaseAddress;
            LogDebug($"Found {name} at P3P.exe+0x{result.Offset:X}.");

            try
            {
                action(address);
            }
            catch (Exception exception)
            {
                LogError($"Failed while resolving {name}.", exception);
            }
        });
    }

    internal static unsafe nint ResolveRelativeCall(nint instruction)
    {
        if (*(byte*)instruction != 0xE8)
            throw new InvalidOperationException($"Expected CALL rel32 at P3P.exe+0x{instruction - BaseAddress:X}.");

        return instruction + 5 + *(int*)(instruction + 1);
    }

    internal static unsafe nint ResolveRelativeJump(nint instruction)
    {
        if (*(byte*)instruction != 0xE9)
            throw new InvalidOperationException($"Expected JMP rel32 at P3P.exe+0x{instruction - BaseAddress:X}.");

        return instruction + 5 + *(int*)(instruction + 1);
    }

    public static unsafe nuint GetGlobalAddress(nuint ptrAddress)
        => (nuint)(*(int*)ptrAddress) + ptrAddress + 4;
}
