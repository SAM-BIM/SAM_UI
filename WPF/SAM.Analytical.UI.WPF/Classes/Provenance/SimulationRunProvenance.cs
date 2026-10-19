// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// The run provenance (roadmap S03) that every Tas, OpenStudio and Both run manifest carries as <c>"provenance"</c>: which build ran,
    /// which engine and producer binaries, which weather and design days, and the settings actually handed to the engine.
    /// <para>
    /// <b>Nothing is assumed or filled in.</b> Every item is read from the running binaries, the files of this run or the settings object the
    /// engine was given. An item that cannot be established is written as <see cref="Unknown"/>, and its reason is recorded under
    /// <c>"unknown"</c> by item path - an item is never blank. The source revision is the build's own stamp (the <c>+</c> build metadata of
    /// its InformationalVersion, which SAM_Deploy installers set); a local Git HEAD is never read, because it need not be what was built.
    /// </para>
    /// <para>
    /// Collecting it must never change how a run ends: each collector catches its own failures and records them as unknown.
    /// </para>
    /// </summary>
    public sealed class SimulationRunProvenance
    {
        /// <summary>Schema identifier of the provenance block, versioned independently of the manifests that carry it.</summary>
        public const string Schema = "sam-ui.run-provenance/1";

        /// <summary>The value written for an item that could not be established; its reason is under <c>"unknown"</c>.</summary>
        public const string Unknown = "unknown";

        private static readonly Regex SourceRevisionPattern = new Regex("^[0-9a-f]{7,64}$", RegexOptions.CultureInvariant);

        private static readonly Regex EnergyPlusVersionPattern = new Regex(@"EnergyPlus,\s*Version\s+([^,]+)", RegexOptions.CultureInvariant);

        private static readonly Dictionary<(string, long, DateTime), string> sha256Cache = new Dictionary<(string, long, DateTime), string>();

        private readonly JsonObject unknown = new JsonObject();

        private readonly JsonArray components = new JsonArray();

        private readonly HashSet<string> componentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <param name="route">The workflow: "Tas", "OpenStudio" or "Both".</param>
        public SimulationRunProvenance(string route)
        {
            Root = new ProvenanceSection(this, new JsonObject(), null);
            Root.Json["schema"] = Schema;
            Root.Json["route"] = route;

            ProvenanceSection application = Root.Section("application");
            Guard(application, "entryAssembly", () => SetAssembly(application.Section("entryAssembly"), Assembly.GetEntryAssembly()));

            //The SAM_UI assembly's own stamp: the content identity of an installed build (SAM_Deploy sets it on every SAM assembly).
            Guard(application, "sourceRevision", () =>
            {
                string? informationalVersion = InformationalVersion(typeof(SimulationRunProvenance).Assembly);
                string? sourceRevision = SourceRevision(informationalVersion);
                application.Set("sourceRevision", sourceRevision, informationalVersion == null
                    ? "this build carries no InformationalVersion, so no source revision was stamped into it (a local developer build); the component SHA-256 values identify the binaries"
                    : "this build's InformationalVersion '" + informationalVersion + "' has no source revision after '+' (a local developer build); the component SHA-256 values identify the binaries");
            });
            application.Json["sourceRevisionOf"] = "the +build metadata of SAM.Analytical.UI.WPF's InformationalVersion; SAM_Deploy installers stamp their SAM_Deploy commit, which pins every repository";
            application.Json["components"] = components;

            AddComponent(typeof(Core.SAMObject).Assembly);
            AddComponent(typeof(AnalyticalModel).Assembly);
            AddComponent(typeof(SimulationRunProvenance).Assembly);
        }

        /// <summary>The provenance object itself; sections are added to it by the run that owns it.</summary>
        public ProvenanceSection Root { get; }

        /// <summary>Records the identity of one loaded SAM assembly the run used (once per assembly).</summary>
        public void AddComponent(Assembly? assembly)
        {
            string name = assembly?.GetName().Name ?? "(none)";
            if (assembly == null || !componentNames.Add(name))
            {
                return;
            }

            ProvenanceSection section = new ProvenanceSection(this, new JsonObject(), string.Format("application.components[{0}]", name));
            Guard(section, "assembly", () => SetAssembly(section, assembly));
            components.Add(section.Json);
        }

        /// <summary>The provenance as written to a manifest; a copy, so the run can keep adding to it.</summary>
        public JsonObject ToJsonObject()
        {
            JsonObject result = (JsonObject)Root.Json.DeepClone();
            result["unknown"] = unknown.DeepClone();
            return result;
        }

        internal void RecordUnknown(string itemPath, string reason)
        {
            unknown[itemPath] = string.IsNullOrWhiteSpace(reason) ? "no reason was given" : reason;
        }

        /// <summary>Runs one collector; whatever it throws is recorded as the reason <paramref name="key"/> is unknown, never passed on.</summary>
        public static void Guard(ProvenanceSection section, string key, Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                section.SetUnknown(key, "it could not be read: " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        /// <summary>Name, version, InformationalVersion, file and SHA-256 of a loaded assembly.</summary>
        public static void SetAssembly(ProvenanceSection section, Assembly? assembly)
        {
            if (assembly == null)
            {
                section.SetUnknown("name", "the process has no managed entry assembly (hosted)");
                return;
            }

            AssemblyName assemblyName = assembly.GetName();
            section.Set("name", assemblyName.Name, "the assembly has no name");
            section.Set("version", assemblyName.Version?.ToString(), "the assembly has no version");
            section.Set("informationalVersion", InformationalVersion(assembly), "the assembly carries no InformationalVersion");

            string location = assembly.IsDynamic ? string.Empty : assembly.Location;
            if (string.IsNullOrWhiteSpace(location))
            {
                section.SetUnknown("path", "the assembly was not loaded from a file");
                section.SetUnknown("sha256", "the assembly was not loaded from a file");
                return;
            }

            section.Json["path"] = location;
            section.Set("sha256", Sha256(location), "the assembly file could not be read");
        }

        /// <summary>
        /// Path, SHA-256, file version and product version of an executable. For a .NET apphost (an .exe beside a .dll of the same name), the
        /// .dll is the code that runs, so it is described as well, as <c>"assembly"</c>.
        /// </summary>
        public static void SetExecutable(ProvenanceSection section, string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                section.SetUnknown("path", "no executable was located");
                return;
            }

            section.Json["path"] = path;
            if (!File.Exists(path))
            {
                section.SetUnknown("sha256", "the executable does not exist at this path");
                return;
            }

            SetFile(section, path!);

            string path_Assembly = Path.ChangeExtension(path!, ".dll");
            if (string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path_Assembly))
            {
                ProvenanceSection assembly = section.Section("assembly");
                assembly.Json["path"] = path_Assembly;
                SetFile(assembly, path_Assembly);
            }
        }

        private static void SetFile(ProvenanceSection section, string path)
        {
            Guard(section, "sha256", () => section.Set("sha256", Sha256(path), "the file could not be read"));
            Guard(section, "fileVersion", () =>
            {
                FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(path);
                section.Set("fileVersion", Blank(fileVersionInfo.FileVersion), "the file has no version resource");
                section.Set("productVersion", Blank(fileVersionInfo.ProductVersion), "the file has no version resource");
            });
        }

        /// <summary>
        /// The Tas engine the in-process Tas route drives: the COM server registered for <c>TBD.Document</c> (what COM starts for the
        /// workflow), its product version and SHA-256.
        /// </summary>
        /// <param name="comServerCommand">The server's registered command line (<see cref="TasComServerCommand"/>); null when none is registered.</param>
        public static void SetTasEngine(ProvenanceSection section, string? comServerCommand)
        {
            section.Json["comServer"] = "TBD.Document";
            string? path = CommandPath(comServerCommand);
            if (path == null)
            {
                section.SetUnknown("version", "no TBD.Document COM server is registered on this machine, so the Tas version and executable could not be read");
                section.SetUnknown("executable", "no TBD.Document COM server is registered on this machine");
                return;
            }

            ProvenanceSection executable = section.Section("executable");
            SetExecutable(executable, path);

            string? version = executable.Json["productVersion"] is JsonValue jsonValue && jsonValue.TryGetValue(out string? productVersion) && productVersion != Unknown ? productVersion : null;
            section.Set("version", version, "the registered Tas executable has no product version (" + path + ")");
        }

        /// <summary>The registered command line of the <c>TBD.Document</c> COM server (LocalServer32), or null. Read only; never throws.</summary>
        public static string? TasComServerCommand()
        {
            try
            {
                using Microsoft.Win32.RegistryKey? progId = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(@"TBD.Document\CLSID");
                string? clsid = progId?.GetValue(null) as string;
                if (string.IsNullOrWhiteSpace(clsid))
                {
                    return null;
                }

                using Microsoft.Win32.RegistryKey? localServer = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(@"CLSID\" + clsid + @"\LocalServer32");
                return localServer?.GetValue(null) as string;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The executable of a registered command line: the quoted part, else up to the first ".exe"; 8.3 names are expanded. Null when none.</summary>
        public static string? CommandPath(string? command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return null;
            }

            command = command!.Trim();
            string path;
            if (command.StartsWith("\"", StringComparison.Ordinal))
            {
                int end = command.IndexOf('"', 1);
                path = end > 1 ? command.Substring(1, end - 1) : command.Trim('"');
            }
            else
            {
                int index = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                path = index >= 0 ? command.Substring(0, index + 4) : command;
            }

            try
            {
                return File.Exists(path) ? new FileInfo(path).FullName : path;
            }
            catch (Exception)
            {
                return path;
            }
        }

        /// <summary>The EnergyPlus version that actually ran, from the first line of its eplusout.err ("Program Version,EnergyPlus, Version x"); null when not there.</summary>
        public static string? EnergyPlusVersionFromErr(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            using StreamReader streamReader = new StreamReader(path!);
            string? line = streamReader.ReadLine();
            Match match = line == null ? Match.Empty : EnergyPlusVersionPattern.Match(line);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        /// <summary>The 7-64 lowercase hex source revision after '+' in an InformationalVersion, or null when there is none.</summary>
        public static string? SourceRevision(string? informationalVersion)
        {
            int plus = informationalVersion?.IndexOf('+') ?? -1;
            if (plus < 0)
            {
                return null;
            }

            string candidate = informationalVersion!.Substring(plus + 1).Trim().ToLowerInvariant();
            return SourceRevisionPattern.IsMatch(candidate) ? candidate : null;
        }

        /// <summary>"sha256:" and the SHA-256 of a SAM object's canonical JSON v1 (SAM_Validation's helper): a content identity for data that is not a file.</summary>
        public static string CanonicalSha256(Core.IJSAMObject jSAMObject)
        {
            JsonObject jsonObject = jSAMObject.ToJsonObject() ?? throw new InvalidOperationException(jSAMObject.GetType().Name + " has no JSON form");
            return Benchmark.BenchmarkCanonicalJson.ComputeSha256(jsonObject.ToJsonString());
        }

        /// <summary>
        /// The provenance a producer reported in its B1a document (samCommit, runnerCommit, engine, route, weather, design days, model hashes,
        /// state), copied as written. Never throws: an unreadable document or one whose provenance is not an object is recorded as unknown.
        /// </summary>
        public static void SetReportedProvenance(ProvenanceSection section, string key, string? benchmarkPath)
        {
            if (string.IsNullOrWhiteSpace(benchmarkPath) || !File.Exists(benchmarkPath))
            {
                section.SetUnknown(key, "the producer wrote no B1a document");
                return;
            }

            JsonNode? provenance;
            try
            {
                provenance = JsonNode.Parse(File.ReadAllText(benchmarkPath!))?["provenance"];
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is System.Text.Json.JsonException || exception is InvalidOperationException)
            {
                section.SetUnknown(key, "the producer's B1a document could not be read: " + exception.GetType().Name + ": " + exception.Message);
                return;
            }

            if (provenance is not JsonObject jsonObject)
            {
                section.SetUnknown(key, "the producer's B1a document has no provenance object");
                return;
            }

            ProvenanceSection reported = section.Section(key);
            foreach (string name in new[] { "samCommit", "runnerCommit", "engine", "route", "weather", "designDaySource", "sourceFileHash", "canonicalModelHash", "canonicalizationVersion", "state" })
            {
                JsonNode? jsonNode = jsonObject[name];
                if (jsonNode == null)
                {
                    reported.SetUnknown(name, "the producer did not report it (absent or null in its B1a document; its warnings and notes say why)");
                }
                else
                {
                    reported.Json[name] = jsonNode.DeepClone();
                }
            }

            //A null inside a reported object (an engine version the producer could not read, say) is the producer's own "not known".
            ReplaceNulls(reported, reported.Json, string.Empty, "the producer reported none (null in its B1a document; its warnings and notes say why)");
        }

        private static void ReplaceNulls(ProvenanceSection section, JsonObject jsonObject, string prefix, string reason)
        {
            foreach (string name in jsonObject.Select(x => x.Key).ToList())
            {
                string key = prefix.Length == 0 ? name : prefix + "." + name;
                if (jsonObject[name] == null)
                {
                    jsonObject[name] = Unknown;
                    section.RecordUnknown(key, reason);
                }
                else if (jsonObject[name] is JsonObject child)
                {
                    ReplaceNulls(section, child, key, reason);
                }
            }
        }

        /// <summary>The environment variables named, as this process passes them to a child: their value, or "not set".</summary>
        public static void SetEnvironment(ProvenanceSection section, params string[] names)
        {
            foreach (string name in names)
            {
                string? value = Environment.GetEnvironmentVariable(name);
                section.Json[name] = string.IsNullOrEmpty(value) ? "not set" : value;
            }
        }

        public static string? InformationalVersion(Assembly assembly)
        {
            return Blank(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        }

        /// <summary>"sha256:" and the SHA-256 of a file's bytes (the B1a notation); cached per path, length and write time.</summary>
        public static string? Sha256(string path)
        {
            FileInfo fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
            {
                return null;
            }

            (string, long, DateTime) key = (fileInfo.FullName.ToLowerInvariant(), fileInfo.Length, fileInfo.LastWriteTimeUtc);
            lock (sha256Cache)
            {
                if (sha256Cache.TryGetValue(key, out string? cached))
                {
                    return cached;
                }
            }

            string result;
            using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                result = "sha256:" + System.Convert.ToHexString(SHA256.HashData(fileStream)).ToLowerInvariant();
            }

            lock (sha256Cache)
            {
                sha256Cache[key] = result;
            }

            return result;
        }

        private static string? Blank(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value!.Trim();
        }
    }

    /// <summary>One object inside a <see cref="SimulationRunProvenance"/>, which knows its item path so an unknown item's reason is filed under it.</summary>
    public sealed class ProvenanceSection
    {
        private readonly SimulationRunProvenance owner;

        internal ProvenanceSection(SimulationRunProvenance owner, JsonObject json, string? path)
        {
            this.owner = owner;
            Json = json;
            Path = path;
        }

        public JsonObject Json { get; }

        /// <summary>The dotted path of this object inside the provenance; null for the provenance itself.</summary>
        public string? Path { get; }

        /// <summary>The child object <paramref name="key"/>, created when absent.</summary>
        public ProvenanceSection Section(string key)
        {
            if (Json[key] is not JsonObject jsonObject)
            {
                jsonObject = new JsonObject();
                Json[key] = jsonObject;
            }

            return new ProvenanceSection(owner, jsonObject, ItemPath(key));
        }

        /// <summary>Writes <paramref name="value"/>, or <see cref="SimulationRunProvenance.Unknown"/> with <paramref name="reasonIfUnknown"/> when it is null or blank.</summary>
        public void Set(string key, string? value, string reasonIfUnknown)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                SetUnknown(key, reasonIfUnknown);
                return;
            }

            Json[key] = value;
        }

        /// <summary>Writes <paramref name="jsonNode"/>, or <see cref="SimulationRunProvenance.Unknown"/> with <paramref name="reasonIfUnknown"/> when it is null.</summary>
        public void Set(string key, JsonNode? jsonNode, string reasonIfUnknown)
        {
            if (jsonNode == null)
            {
                SetUnknown(key, reasonIfUnknown);
                return;
            }

            Json[key] = jsonNode;
        }

        public void SetUnknown(string key, string reason)
        {
            Json[key] = SimulationRunProvenance.Unknown;
            RecordUnknown(key, reason);
        }

        /// <summary>Files a reason under this section's item <paramref name="key"/> without changing its value (for a value written by someone else).</summary>
        public void RecordUnknown(string key, string reason)
        {
            owner.RecordUnknown(ItemPath(key), reason);
        }

        private string ItemPath(string key)
        {
            return Path == null ? key : Path + "." + key;
        }
    }
}
