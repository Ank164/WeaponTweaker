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
            var secondMasterPath = Path.Combine(folder, "SecondMaster.esm");
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

            var secondMaster = new SkyrimMod(ModKey.FromFileName("SecondMaster.esm"), SkyrimRelease.SkyrimSE) { IsMaster = true };
            var secondMasterWeapon = secondMaster.Weapons.AddNew();
            secondMasterWeapon.EditorID = "WT_TestAxe";
            secondMasterWeapon.Name = "Test Axe";
            secondMasterWeapon.BasicStats = new WeaponBasicStats { Damage = 14, Weight = 12, Value = 65 };
            secondMasterWeapon.Data = new WeaponData { Speed = 0.8f, Reach = 1.0f };
            secondMasterWeapon.Critical = new CriticalData { Damage = 7 };
            secondMaster.BeginWrite.ToPath(secondMasterPath).WithNoLoadOrder().Write();

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

            var secondRow = new WeaponRow
            {
                Source = secondMasterWeapon, FormKey = secondMasterWeapon.FormKey, Name = secondMasterWeapon.Name?.String ?? "",
                EditorID = secondMasterWeapon.EditorID ?? "", Damage = 30, OriginalDamage = 14,
                Speed = 0.9f, OriginalSpeed = 0.8f, Reach = 1.0f, OriginalReach = 1.0f,
                Weight = 11, OriginalWeight = 12, Value = 90, OriginalValue = 65,
                CriticalDamage = 15, OriginalCriticalDamage = 7
            };
            // Put the new master before the existing one deliberately. Append must still
            // preserve the existing patch's master indices and add SecondMaster.esm last.
            var conflictingOrder = new[] { secondMaster.ModKey }.Concat(order).ToArray();
            var backupPath = MainForm.WritePatch(patchPath, [secondRow], conflictingOrder, folder);

            using var result = MainForm.OpenPlugin(patchPath);
            var patched = result.Weapons.Single(x => x.FormKey == masterWeapon.FormKey);
            var secondPatched = result.Weapons.Single(x => x.FormKey == secondMasterWeapon.FormKey);
            var ok = patched.BasicStats?.Damage == 25 && patched.Data?.Speed == 1.35f &&
                     patched.Data?.Reach == 1.1f && patched.BasicStats?.Weight == 7 &&
                     patched.BasicStats?.Value == 80 && patched.Critical?.Damage == 12 &&
                     secondPatched.BasicStats?.Damage == 30 && secondPatched.Data?.Speed == 0.9f &&
                     result.MasterReferences.First().Master == master.ModKey &&
                     result.MasterReferences.Last().Master == secondMaster.ModKey &&
                     result.IsSmallMaster && result.MasterReferences.Any(x => x.Master == master.ModKey) &&
                     backupPath is not null && File.Exists(backupPath) && File.Exists(sourcePath) &&
                     File.Exists(masterPath) && File.Exists(secondMasterPath);

            var profile = Path.Combine(folder, "Profile");
            Directory.CreateDirectory(profile);
            File.WriteAllLines(Path.Combine(profile, "plugins.txt"), ["# generated", "*ActiveWeapons.esp", "InactiveWeapons.esp"]);
            File.WriteAllLines(Path.Combine(profile, "loadorder.txt"), ["Skyrim.esm", "InactiveWeapons.esp", "ActiveWeapons.esp"]);
            var parsed = MainForm.ReadMo2ProfileLoadOrder(profile, [ModKey.FromFileName("Skyrim.esm")]);
            ok &= parsed.Select(x => x.FileName.String).SequenceEqual(["Skyrim.esm", "ActiveWeapons.esp"]);

            var instance = Path.Combine(folder, "MO2");
            var instanceProfile = Path.Combine(instance, "profiles", "Test Profile");
            var game = Path.Combine(instance, "Stock Game");
            Directory.CreateDirectory(instanceProfile);
            Directory.CreateDirectory(Path.Combine(game, "Data"));
            File.WriteAllText(Path.Combine(instance, "ModOrganizer.ini"), $"gamePath=@ByteArray({game.Replace("\\", "\\\\")})");
            ok &= MainForm.ResolveMo2DataFolder(instanceProfile) == Path.Combine(game, "Data");
            var selectedCopy = Path.Combine(folder, "Shadowed", "ActiveWeapons.esp");
            Directory.CreateDirectory(Path.GetDirectoryName(selectedCopy)!);
            File.WriteAllText(selectedCopy, "shadowed");
            var activeCopy = Path.Combine(game, "Data", "ActiveWeapons.esp");
            File.WriteAllText(activeCopy, "active");
            ok &= MainForm.ResolveActivePluginPath(selectedCopy, instanceProfile) == Path.GetFullPath(activeCopy);
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
