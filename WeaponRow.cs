using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace WeaponTweaker;

internal sealed class WeaponRow
{
    public required IWeaponGetter Source { get; init; }
    public required FormKey FormKey { get; init; }
    public string Name { get; init; } = "";
    public string EditorID { get; init; } = "";
    public int Damage { get; set; }
    public float Speed { get; set; }
    public float Reach { get; set; }
    public float Weight { get; set; }
    public uint Value { get; set; }
    public int CriticalDamage { get; set; }

    public int OriginalDamage { get; init; }
    public float OriginalSpeed { get; init; }
    public float OriginalReach { get; init; }
    public float OriginalWeight { get; init; }
    public uint OriginalValue { get; init; }
    public int OriginalCriticalDamage { get; init; }

    public bool Changed => Damage != OriginalDamage || Speed != OriginalSpeed || Reach != OriginalReach ||
                           Weight != OriginalWeight || Value != OriginalValue || CriticalDamage != OriginalCriticalDamage;
}
