using System;
using TaleWorlds.Localization;

namespace TheFamilyWeChoose;

public static class Extensions
{
    public static TextObject ToTextObject(this SexualOrientation sexualOrientation)
    {
        return sexualOrientation switch
        {
            SexualOrientation.Heterosexual => new TextObject("{=TFWCHeterosexual}Heterosexual"),
            SexualOrientation.Bisexual => new TextObject("{=TFWCBisexual}Bisexual"),
            SexualOrientation.Homosexual => new TextObject("{=TFWCHomosexual}Homosexual"),
            _ => throw new ArgumentOutOfRangeException(nameof(sexualOrientation), sexualOrientation, "Unknown sexual orientation")
        };
    }
}