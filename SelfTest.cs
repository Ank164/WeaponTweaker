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
            var masterPath = Path.Combine(folder, "TestMaster.esm");
            var sourcePath = Path.Combine(folder, "TestWeapons.esp");
            var patchPath = Path.Combine(folder, "TestWeapons - Weapon Tweaks.esp");
            var master = new SkyrimMod(ModKey.FromFileName("TestMaster.esm"), SkyrimRelease.SkyrimSE) { IsMaster = true };
            var masterWeapon = master.Weapons.AddNew();
            masterWeapon.EditorID = "WT_TestSword";
            masterWeapon.Name = "Test Sword";
            masterWeapon.BasicStats = new WeaponBasicStats { Damage = 10, Weight = 8, Value = 50 };
            masterWeapon.Data = new WeaponData { Speed = 1.0f, Reach = 1.0f };
            masterWeapon.Critical = new CriticalData { Damage = 5 };
            master.BeginWrite.ToPath(masterPath).WithNoLoadOrder().Write();

            var source = new SkyrimMod(ModKey.FromFileName("TestWeapons.esp"), SkyrimRelease.SkyrimSE);
            source.Weapons.GetOrAddAsOverride(masterWeapon).BasicStats!.Damage = 11;
            source.BeginWrite.ToPath(sourcePath).WithLoadOrder(master).Write();

            using var loaded = MainForm.OpenPlugin(sourcePath);
            var loadedWeapon = loaded.Weapons.Single();
            var row = new WeaponRow
            {
                Source = loadedWeapon, FormKey = loadedWeapon.FormKey, Name = loadedWeapon.Name?.String ?? "",
                EditorID = loadedWeapon.EditorID ?? "", Damage = 25, OriginalDamage = 11,
                Speed = 1.35f, OriginalSpeed = 1.0f, Reach = 1.1f, OriginalReach = 1.0f,
                Weight = 7, OriginalWeight = 8, Value = 80, OriginalValue = 50,
                CriticalDamage = 12, OriginalCriticalDamage = 5
            };
            var order = loaded.MasterReferences.Select(x => x.Master).Append(loaded.ModKey).ToArray();
            MainForm.WritePatch(patchPath, [row], order, folder);

            using var result = MainForm.OpenPlugin(patchPath);
            var patched = result.Weapons.Single();
            var ok = patched.BasicStats?.Damage == 25 && patched.Data?.Speed == 1.35f &&
                     patched.Data?.Reach == 1.1f && patched.BasicStats?.Weight == 7 &&
                     patched.BasicStats?.Value == 80 && patched.Critical?.Damage == 12 &&
                     result.IsSmallMaster && result.MasterReferences.Any(x => x.Master == master.ModKey) &&
                     File.Exists(sourcePath) && File.Exists(masterPath);

            var profile = Path.Combine(folder, "Profile");
            Directory.CreateDirectory(profile);
            File.WriteAllLines(Path.Combine(profile, "plugins.txt"), ["# generated", "*ActiveWeapons.esp", "InactiveWeapons.esp"]);
            File.WriteAllLines(Path.Combine(profile, "loadorder.txt"), ["Skyrim.esm", "InactiveWeapons.esp", "ActiveWeapons.esp"]);
            var parsed = MainForm.ReadMo2ProfileLoadOrder(profile, [ModKey.FromFileName("Skyrim.esm")]);
            ok &= parsed.Select(x => x.FileName.String).SequenceEqual(["Skyrim.esm", "ActiveWeapons.esp"]);
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
