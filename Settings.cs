using System;
using MCM.Common;
using TaleWorlds.Localization;

namespace TheFamilyWeChoose;

internal class Settings : ICloneable
{
    internal SexualOrientation SexualOrientation => (SexualOrientation)SexualOrientationDropdown.SelectedIndex;
    internal bool AllowConsort { get; set; }
    internal float ConsortPenalty { get; set; } = 2f;
    internal bool RemoveCooldown { get; set; }

    internal Dropdown<TextObject> SexualOrientationDropdown { get; set; } = new([
        SexualOrientation.Heterosexual.ToTextObject(),
        SexualOrientation.Bisexual.ToTextObject(),
        SexualOrientation.Homosexual.ToTextObject()
    ], 0);

    public Settings()
    {
    }

    public Settings(Settings other)
    {
        AllowConsort = other.AllowConsort;
        ConsortPenalty = other.ConsortPenalty;
        SexualOrientationDropdown = other.SexualOrientationDropdown.Clone() as Dropdown<TextObject>;
    }

    public object Clone() => new Settings(this);
}