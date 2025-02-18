using MCM.Common;
using TaleWorlds.Localization;

namespace TheFamilyWeChoose;

internal class Settings
{
    internal SexualOrientation SexualOrientation => (SexualOrientation)SexualOrientationDropdown.SelectedIndex;
    internal bool AllowConsort { get; set; } = false;
    internal float ConsortPenalty { get; set; } = 2f;

    internal Dropdown<TextObject> SexualOrientationDropdown { get; set; } = new([
        SexualOrientation.Heterosexual.ToTextObject(),
        SexualOrientation.Bisexual.ToTextObject(),
        SexualOrientation.Homosexual.ToTextObject()
    ], 0);
}