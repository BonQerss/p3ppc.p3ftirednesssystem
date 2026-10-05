using Reloaded.Mod.Interfaces;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace p3ppc.p3ftirednesssystem.Template;

public class ModContext
{
    public IModLoader ModLoader { get; set; } = null!;
    public IReloadedHooks? Hooks { get; set; } = null!;
    public ILogger Logger { get; set; } = null!;
    public Config Configuration { get; set; } = null!;
    public IModConfig ModConfig { get; set; } = null!;
    public IMod Owner { get; set; } = null!;
}
