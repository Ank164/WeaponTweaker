using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace WeaponTweaker;

internal static class SelfTest
{
    public static int Run()
    {
        var folder = Path.Combine(Path.GetTempPath(), "WeaponTweakerSelfTest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var sourcePath = Path.Combine(folder, "TestWeapons.esp");
            var patchPath = Path.Combine(folder, "TestWeapons - Weapon Tweaks.esp");
            var source = new SkyrimMod(ModKey.FromFileName("TestWeapons.esp"), SkyrimRelease.SkyrimSE);
            var weapon = source.Weapons.AddNew();
            weapon.EditorID = "WT_TestSword";
            weapon.Name = "Test Sword";
            weapon.BasicStats = new WeaponBasicStats { Damage = 10, Weight = 8, Value = 50 };
            weapon.Data = new WeaponData { Speed = 1.0f, Reach = 1.0f };
            weapon.Critical = new CriticalData { Damage = 5 };
            source.BeginWrite.ToPath(sourcePath).WithNoLoadOrder().Write();

            using var loaded = MainForm.OpenPlugin(sourcePath);
            var loadedWeapon = loaded.Weapons.Single();
            var row = new WeaponRow
            {
                Source = loadedWeapon, FormKey = loadedWeapon.FormKey, Name = loadedWeapon.Name?.String ?? "",
                EditorID = loadedWeapon.EditorID ?? "", Damage = 25, OriginalDamage = 10,
                Speed = 1.35f, OriginalSpeed = 1.0f, Reach = 1.1f, OriginalReach = 1.0f,
                Weight = 7, OriginalWeight = 8, Value = 80, OriginalValue = 50,
                CriticalDamage = 12, OriginalCriticalDamage = 5
            };
            MainForm.WritePatch(patchPath, [row], loaded);

            using var result = MainForm.OpenPlugin(patchPath);
            var patched = result.Weapons.Single();
            var ok = patched.BasicStats?.Damage == 25 && patched.Data?.Speed == 1.35f &&
                     patched.Data?.Reach == 1.1f && patched.BasicStats?.Weight == 7 &&
                     patched.BasicStats?.Value == 80 && patched.Critical?.Damage == 12 &&
                     result.IsSmallMaster && File.Exists(sourcePath);
            Console.WriteLine(ok ? "SELF-TEST PASSED" : "SELF-TEST FAILED");
            return ok ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch { }
        }
    }
}
