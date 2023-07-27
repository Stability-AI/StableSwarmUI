using FreneticUtilities.FreneticDataSyntax;
using StableSwarmUI.DataHolders;
using StableSwarmUI.Backends;

namespace StableSwarmUI.Builtin_AutoWebUIExtension;

public class AutoWebUIAPIBackend : AutoWebUIAPIAbstractBackend
{
    public class AutoWebUIAPISettings : AutoConfiguration
    {
        /// <summary>Base web address of the auto webui instance.</summary>
        [SuggestionPlaceholder(Text = "WebUI's address...")]
        [ConfigComment("The address of the WebUI, eg 'http://localhost:7860'.")]
        public string Address = "";
    }

    public override string Address => (SettingsRaw as AutoWebUIAPISettings).Address.TrimEnd('/');

    public override Task Init()
    {
        return InitInternal(false);
    }
}
