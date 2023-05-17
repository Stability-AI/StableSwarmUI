using FreneticUtilities.FreneticDataSyntax;
using StableUI.DataHolders;
using StableUI.Backends;

namespace StableUI.Builtin_AutoWebUIExtension;

public class AutoWebUIAPIBackend : AutoWebUIAPIAbstractBackend<AutoWebUIAPIBackend.AutoWebUIAPISettings>
{
    public class AutoWebUIAPISettings : AutoConfiguration
    {
        /// <summary>Base web address of the auto webui instance.</summary>
        [SuggestionPlaceholder(Text = "WebUI's address...")]
        [ConfigComment("The address of the WebUI, eg 'http://localhost:7860'.")]
        public string Address = "";
    }

    public override string Address => Settings.Address;

    public override Task Init()
    {
        return InitInternal(false);
    }
}
