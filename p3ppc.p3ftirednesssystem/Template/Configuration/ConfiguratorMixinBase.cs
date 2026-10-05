using Reloaded.Mod.Interfaces;

namespace p3ppc.p3ftirednesssystem.Template.Configuration;

public class ConfiguratorMixinBase
{
    public virtual IUpdatableConfigurable[] MakeConfigurations(string configFolder)
        => new IUpdatableConfigurable[]
        {
            Configurable<Config>.FromFile(Path.Combine(configFolder, "Config.json"), "Default Config")
        };

    public virtual bool TryRunCustomConfiguration(Configurator configurator) => false;

    public virtual void Migrate(string oldDirectory, string newDirectory) { }
}
