using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

[SetUpFixture]
public sealed class Setup : Rhino.Testing.Fixtures.RhinoSetupFixture { }

[TestFixture]
public class IconTests
{
    // SAM_ICON_MANIFESTS = ';'-separated paths to <repo>/design/grasshopper-icons/manifest.json
    [Test]
    public void EveryManifestObject_LoadsInGrasshopper_WithItsRedesignedIcon()
    {
        var server = Grasshopper.Instances.ComponentServer;
        Assert.That(server, Is.Not.Null);
        var failures = new List<string>();
        var report = new List<string>();
        foreach (string manifest in (Environment.GetEnvironmentVariable("SAM_ICON_MANIFESTS") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string repo = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifest), "..", ".."));
            var rows = JsonDocument.Parse(File.ReadAllText(manifest)).RootElement.EnumerateArray().ToList();
            int ok = 0; int globalMax = 0;
            foreach (var r in rows)
            {
                string guid = r.GetProperty("guid").GetString(), cls = r.GetProperty("class").GetString();
                string png = Path.Combine(repo, r.GetProperty("project_dir").GetString(), "Resources", "Icons", r.GetProperty("resource").GetString() + ".png");
                var obj = server.EmitObject(new Guid(guid));
                if (obj == null) { failures.Add($"{Path.GetFileName(repo)} {cls} {guid}: not loaded by Grasshopper"); continue; }
                string name = r.GetProperty("name").GetString() ?? "";
                if (!name.StartsWith("{") && obj.Name != name) failures.Add($"{cls}: name '{obj.Name}' != source '{name}'");
                string cat = r.GetProperty("category").ValueKind == JsonValueKind.String ? r.GetProperty("category").GetString() : null;
                if (cat != null && !cat.StartsWith("{") && obj.Category != cat) failures.Add($"{cls}: category '{obj.Category}' != source '{cat}'");
                Bitmap icon = obj.Icon_24x24 as Bitmap;
                if (icon == null) { failures.Add($"{cls}: Icon_24x24 null"); continue; }
                if (icon.Width != 24 || icon.Height != 24) { failures.Add($"{cls}: icon {icon.Width}x{icon.Height}"); continue; }
                using var expected = new Bitmap(png);
                int maxd = 0;
                for (int y = 0; y < 24; y++)
                    for (int x = 0; x < 24; x++)
                    {
                        Color a = icon.GetPixel(x, y), b = expected.GetPixel(x, y);
                        int d = Math.Abs(a.A - b.A);
                        if (a.A > 0 && b.A > 0) d = Math.Max(d, Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B))));
                        maxd = Math.Max(maxd, d);
                    }
                string dump = Environment.GetEnvironmentVariable("SAM_ICON_DUMP");
                if (!string.IsNullOrEmpty(dump)) { Directory.CreateDirectory(dump); icon.Save(Path.Combine(dump, cls + ".png")); }
                globalMax = Math.Max(globalMax, maxd);
                if (maxd > 2) { failures.Add($"{cls}: icon differs from {Path.GetFileName(png)} (max channel diff {maxd})"); continue; }
                ok++;
            }
            report.Add($"{Path.GetFileName(repo)}: {ok}/{rows.Count} objects load in Grasshopper with their redesigned 24x24 icon (max channel diff vs manifest PNG: {globalMax}, premultiplied-alpha rounding; tolerance 2)");
        }
        foreach (var l in report) TestContext.Progress.WriteLine(l);
        foreach (var f in failures) TestContext.Progress.WriteLine("FAIL " + f);
        File.WriteAllLines(Path.Combine(TestContext.CurrentContext.WorkDirectory, "icon_report.txt"), report.Concat(failures.Select(f => "FAIL " + f)));
        Assert.That(report, Is.Not.Empty);
        Assert.That(failures, Is.Empty);
    }
}
