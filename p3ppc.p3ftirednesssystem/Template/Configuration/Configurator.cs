using Reloaded.Mod.Interfaces;

namespace p3ppc.p3ftirednesssystem.Template.Configuration;

public class Configurator : IConfiguratorV3
{
    private static readonly ConfiguratorMixin ConfiguratorMixin = new();

    public string? ModFolder { get; private set; }
    public string? ConfigFolder { get; private set; }
    public ConfiguratorContext Context { get; private set; }

    public IUpdatableConfigurable[] Configurations => _configurations ?? MakeConfigurations();
    private IUpdatableConfigurable[]? _configurations;

    private IUpdatableConfigurable[] MakeConfigurations()
    {
        _configurations = ConfiguratorMixin.MakeConfigurations(ConfigFolder!);
        for (int x = 0; x < Configurations.Length; x++)
        {
            int copy = x;
            Configurations[x].ConfigurationUpdated += configurable => Configurations[copy] = configurable;
        }
        return _configurations;
    }

    public Configurator() { }
    public Configurator(string configDirectory) => ConfigFolder = configDirectory;

    public void Migrate(string oldDirectory, string newDirectory)
        => ConfiguratorMixin.Migrate(oldDirectory, newDirectory);

    public TType GetConfiguration<TType>(int index) => (TType)Configurations[index];
    public void SetConfigDirectory(string configDirectory) => ConfigFolder = configDirectory;
    public void SetContext(in ConfiguratorContext context) => Context = context;
    public IConfigurable[] GetConfigurations() => Configurations;
    public bool TryRunCustomConfiguration() => ConfiguratorMixin.TryRunCustomConfiguration(this);
    public void SetModDirectory(string modDirectory) => ModFolder = modDirectory;
}
