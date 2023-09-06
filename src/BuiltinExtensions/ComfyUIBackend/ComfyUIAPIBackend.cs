
using FreneticUtilities.FreneticDataSyntax;
using StableSwarmUI.DataHolders;

namespace StableSwarmUI.Builtin_ComfyUIBackend;

public class ComfyUIAPIBackend : ComfyUIAPIAbstractBackend
{
    public class ComfyUIAPISettings : AutoConfiguration
    {
        /// <summary>Base web address of the ComfyUI instance.</summary>
        [SuggestionPlaceholder(Text = "ComfyUI's address...")]
        [ConfigComment("The address of the ComfyUI instance, eg 'http://localhost:8188'.")]
        public string Address = "";

        [ConfigComment("Whether the backend is allowed to revert to an 'idle' state if the API address is unresponsive.\nAn idle state is not considered an error, but cannot generate.\nIt will automatically return to 'running' if the API becomes available.")]
        public bool AllowIdle = false;
    }

    public override string Address => (SettingsRaw as ComfyUIAPISettings).Address.TrimEnd('/');

    public override bool CanIdle => (SettingsRaw as ComfyUIAPISettings).AllowIdle;

    public override Task Init()
    {
        return InitInternal(CanIdle);
    }
}
