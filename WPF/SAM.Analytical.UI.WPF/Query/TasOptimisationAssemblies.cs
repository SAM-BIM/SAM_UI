// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

extern alias SAMMath;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Query
    {
        /// <summary>The assemblies Simulate &gt; Optimisation needs beyond the rest of SAM_UI.</summary>
        public static readonly IReadOnlyList<string> TasOptimisationAssemblyNames = Array.AsReadOnly(new[] { "SAM.Math", "SAM.Analytical.Tas.GenOpt", "SAM.Core.Optimisation" });

        /// <summary>
        /// True when <paramref name="exception"/> (or an inner one) is an assembly or type load failure: the symptom of a
        /// missing SAM.Analytical.Tas.GenOpt.dll or SAM.Core.Optimisation.dll, or of a stale SAM.Math.dll (one without the
        /// optimisation kernel) or SAM.Analytical.Tas.GenOpt.dll (one without the Optimisation Definition adapter) loaded
        /// from an older SAM install. Every SAM assembly is version 1.0.0.0, so a stale copy loads silently and only
        /// fails when a newer type or member is reached.
        /// </summary>
        public static bool IsTasOptimisationLoadFailure(Exception? exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is TypeLoadException || current is MissingMemberException || current is FileLoadException || current is BadImageFormatException)
                {
                    return true;
                }

                if (current is FileNotFoundException fileNotFoundException && IsAssemblyName(fileNotFoundException.FileName))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// What to tell the user about a load failure: the error, where each optimisation assembly was loaded from, and
        /// the application folder. It references no SAM.Math or GenOpt type, so it works when those cannot be loaded.
        /// </summary>
        public static string TasOptimisationLoadFailure(Exception? exception)
        {
            List<string> lines = new List<string>
            {
                "Simulate > Optimisation could not load its assemblies. It needs a current SAM.Analytical.Tas.GenOpt.dll, SAM.Core.Optimisation.dll and a current SAM.Math.dll (one with the SAM optimisation kernel) beside the application. A stale SAM.Math.dll from an older SAM install is the known cause; this is a deployment problem, not a problem with the model.",
                string.Empty,
                "Error: " + Innermost(exception),
            };

            lines.Add(TasOptimisationAssemblyLocations());
            lines.Add("Application folder: " + AppContext.BaseDirectory);

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>Where each optimisation assembly is loaded from in this process ("not loaded" when it is not).</summary>
        public static string TasOptimisationAssemblyLocations()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            List<string> texts = new List<string>();
            foreach (string name in TasOptimisationAssemblyNames)
            {
                Assembly? assembly = assemblies.FirstOrDefault(x => string.Equals(x.GetName().Name, name, StringComparison.OrdinalIgnoreCase));
                texts.Add(name + ": " + (assembly == null ? "not loaded" : (string.IsNullOrEmpty(assembly.Location) ? "(no file)" : assembly.Location)));
            }

            return "Loaded: " + string.Join("; ", texts);
        }

        /// <summary>
        /// Reaches the optimisation kernel and the SAM_Tas native adapter once, so a stale or missing assembly is found when
        /// the window opens rather than in the middle of a run. Null when both load; otherwise the load failure.
        /// </summary>
        public static Exception? TasOptimisationAssemblyFailure()
        {
            try
            {
                Probe();
                return null;
            }
            catch (Exception exception) when (IsTasOptimisationLoadFailure(exception))
            {
                return exception;
            }
        }

        // Its own method, never inlined: a missing type fails this method's compilation, which the caller catches.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Probe()
        {
            GC.KeepAlive(new SAMMath::SAM.Math.GoldenSection());
            GC.KeepAlive(global::SAM.Analytical.Tas.GenOpt.Convert.NativeAlgorithmTypes);
            GC.KeepAlive(typeof(global::SAM.Analytical.Tas.GenOpt.NativeGenOptRun));
            // PR6: a SAM.Analytical.Tas.GenOpt.dll from before the shared result rules is stale too.
            GC.KeepAlive(typeof(global::SAM.Analytical.Tas.GenOpt.NativeGenOptOutcome));
            GC.KeepAlive(typeof(SAMMath::SAM.Math.OptimisationProgress));
            // PR5a: the form edits a SAM.Core.Optimisation definition, which SAM_Tas' adapter (native Optimisation PR4) runs.
            GC.KeepAlive(typeof(global::SAM.Core.Optimisation.OptimisationDefinition));
            GC.KeepAlive(global::SAM.Analytical.Tas.GenOpt.Query.TasOptimisationCapabilities());
            GC.KeepAlive(typeof(global::SAM.Analytical.Tas.GenOpt.TasOptimisationDefinitionException));
        }

        private static bool IsAssemblyName(string? fileName)
        {
            return !string.IsNullOrWhiteSpace(fileName) && (fileName!.IndexOf("Version=", StringComparison.OrdinalIgnoreCase) >= 0 || fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
        }

        private static string Innermost(Exception? exception)
        {
            if (exception == null)
            {
                return "(none)";
            }

            Exception current = exception;
            while (current.InnerException != null && !(current is TypeLoadException || current is MissingMemberException || current is FileLoadException || current is FileNotFoundException))
            {
                current = current.InnerException;
            }

            return current.GetType().Name + ": " + current.Message;
        }
    }
}
