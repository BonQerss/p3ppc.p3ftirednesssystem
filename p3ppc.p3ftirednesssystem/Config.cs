using p3ppc.p3ftirednesssystem.Template.Configuration;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace p3ppc.p3ftirednesssystem;

public class Config : Configurable<Config>
{
    [DisplayName("Debug Logging")]
    [Description("Logs fatigue, sickness, endurance, presentation, and party-departure state changes.")]
    [DefaultValue(false)]
    [JsonPropertyName("DebugLogging")]
    public bool DebugEnabled { get; set; } = false;

    [DisplayName("Fatigue Rate Multiplier")]
    [Description("Changes how quickly endurance drains.")]
    [DefaultValue(1)]
    public int FatigueDrainMultiplier { get; set; } = 1;
}

public class ConfiguratorMixin : ConfiguratorMixinBase
{
}
