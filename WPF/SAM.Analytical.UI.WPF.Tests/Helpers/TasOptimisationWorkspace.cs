// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// A short, unique temporary Tas project folder for the Optimisation tests: one placeholder Tas file (so the folder
    /// check is satisfied and the snapshot has something to copy) and a Script.txt that is a JSON spec for SAM_Tas'
    /// StubTasGenExecute (SAM_Tas#86), which stands in for TasGenExecute.exe. Deleted on dispose.
    /// </summary>
    public sealed class TasOptimisationWorkspace : IDisposable
    {
        public TasOptimisationWorkspace(string script = null, bool tasFile = true)
        {
            Directory = Path.Combine(Path.GetTempPath(), "SAMOptUI", Guid.NewGuid().ToString("N").Substring(0, 10));
            System.IO.Directory.CreateDirectory(Directory);

            if (tasFile)
            {
                File.WriteAllText(Path.Combine(Directory, "Model.tbd"), "placeholder");
            }

            ScriptPath = Path.Combine(Directory, "Script.txt");
            File.WriteAllText(ScriptPath, script ?? StubScript(new[] { 4.968943799848584 }));
        }

        public string Directory { get; }

        public string ScriptPath { get; }

        /// <summary>SAM_Tas' default parent of the run folders.</summary>
        public string RunsDirectory => Path.Combine(Directory, "SAM_NativeGenOpt");

        /// <summary>The protocol stand-in, copied next to the test assembly by its ProjectReference.</summary>
        public static string StubExecutable => Path.Combine(AppContext.BaseDirectory, "StubTasGenExecute.exe");

        /// <summary>The form of an example, pointed at this workspace.</summary>
        public TasOptimisationInput Input(TasOptimisationExample tasOptimisationExample = TasOptimisationExample.SystemsDemoGoldenSection)
        {
            TasOptimisationInput result = TasOptimisationInput.Create(tasOptimisationExample);
            result.Directory = Directory;
            result.ScriptPath = ScriptPath;
            return result;
        }

        /// <summary>The evaluation folders of every run in this workspace, sorted, e.g. "0001", "0002".</summary>
        public List<string> EvaluationFolders()
        {
            if (!System.IO.Directory.Exists(RunsDirectory))
            {
                return new List<string>();
            }

            return System.IO.Directory.GetDirectories(RunsDirectory)
                .SelectMany(x => System.IO.Directory.Exists(Path.Combine(x, "evaluations")) ? System.IO.Directory.GetDirectories(Path.Combine(x, "evaluations")) : new string[0])
                .Select(Path.GetFileName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>The working folder of one evaluation ("0002"), or null while it does not exist.</summary>
        public string EvaluationFolder(string name)
        {
            if (!System.IO.Directory.Exists(RunsDirectory))
            {
                return null;
            }

            return System.IO.Directory.GetDirectories(RunsDirectory, name, SearchOption.AllDirectories).FirstOrDefault();
        }

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(Directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// A stub spec: quadratic objective sum((x_i - c_i)^2), written as "Result"; output k &gt; 0 is k times the
        /// coordinate sum. <paramref name="modes"/> and <paramref name="sleepMs"/> are keyed by simulation number.
        /// </summary>
        public static string StubScript(double[] center, IEnumerable<string> outputs = null, IDictionary<string, string> modes = null, IDictionary<string, int> sleepMs = null)
        {
            return JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["kind"] = "quadratic",
                ["center"] = center ?? new double[0],
                ["weight"] = new double[0],
                ["offset"] = 0.0,
                ["quantum"] = 1.0,
                ["outputs"] = outputs ?? new[] { "Result", "Cost", "CO2" },
                ["modes"] = modes ?? new Dictionary<string, string>(),
                ["sleepMs"] = sleepMs ?? new Dictionary<string, int>(),
                ["format"] = "R",
                ["datePrefix"] = true,
            });
        }
    }
}
