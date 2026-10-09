// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.GenOpt;
using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// The "tas-model" journey without Tas (PR8): an inventory shaped like the Systems Demo (as SAM_Tas' PR7b fixtures
    /// and licensed smoke read it), a glazing pool with generic names and the PR7a-2 values, a session whose model and
    /// glazing readers are these stand-ins, and the stub TasGenExecute's spec for a generated script
    /// (<c>SAM_TAS_GENOPT_STUB_SPEC</c>, SAM_Tas PR7b-1).
    /// </summary>
    public static class TasModelJourneyFixtures
    {
        public const string Glazing = "Suncool Example";

        public const string StubSpecVariable = "SAM_TAS_GENOPT_STUB_SPEC";

        /// <summary>A Systems Demo-shaped inventory whose files are named as in a <see cref="TasOptimisationWorkspace"/> (Model.tbd …).</summary>
        public static TasModelInventory Demo(string tbd = "Model.tbd", string? tsd = "Model.tsd", string? tpd = "Model.tpd")
        {
            return new TasModelInventory(
                tbd,
                tsd,
                tpd,
                new[]
                {
                    new TasInternalConditionInfo("Office Weekday", "Office", 17, new TasSetpointProfile(TasSetpointProfileType.Value, 1, 20), new TasSetpointProfile(TasSetpointProfileType.Value, 1, 24)),
                    new TasInternalConditionInfo("Steady State Heating ", null, 26, new TasSetpointProfile(TasSetpointProfileType.Value, 1, 21), null),
                },
                new[]
                {
                    new TasGlazingConstructionInfo(Glazing, new[] { "Lower Window-pane", "Upper Window-pane", "Curtain Wall-pane" }, 0.33669999241828918, 1.0549999475479126, 0.64200001955032349),
                },
                tpd == null ? null : new[]
                {
                    new TasPlantRoomInfo("Plant Room", new[]
                    {
                        new TasPlantControllerInfo("HeatPumpController", "tpdTempSensor", 3),
                    }),
                })
            {
                TpdCostUnit = tpd == null ? null : "£",
                HeatingDemand = 15227.840663709641,
                CoolingDemand = 2978.5325259896517,
                PlantEnergy = tpd == null ? (double?)null : 27387.79,
                PlantCost = tpd == null ? (double?)null : 7363.17,
                PlantCO2 = tpd == null ? (double?)null : 3932.89,
            };
        }

        /// <summary>
        /// A glazing pool (generic names, PR7a-2 values), each system with a SAM window system attached so the runner can
        /// name its TBD pane. With the Demo's glazing (g 0.337, U 1.055, light 0.642) and the default filter, "Triple
        /// low-e" and "Double B" pass (Ug ≤ 1.355, light ≥ 0.542); the others fail Ug or light.
        /// </summary>
        public static List<TasGlazingSystem> Pool()
        {
            return new List<TasGlazingSystem>
            {
                System("Triple low-e", "My glazing systems", 0.36886733770370483, 0.997646152973175, 0.7281997203826904, "aaaaaaaa-0000-0000-0000-000000a5191c"),
                System("Double B", "My glazing systems", 0.28368183970451355, 1.3, 0.7965357303619385, "aaaaaaaa-0000-0000-0000-000000ff1c5e"),
                System("Library single", "Default library", 0.7917975187301636, 5.555555820465088, 0.8700000047683716, "aaaaaaaa-0000-0000-0000-00000055cd3f"),
                System("Solar control A", "Default library", 0.11761055886745453, 2.342884063720703, 0.07330752164125443, "aaaaaaaa-0000-0000-0000-00000036191d"),
            };
        }

        public static TasGlazingSystem System(string name, string source, double g, double ug, double light, string guid)
        {
            return new TasGlazingSystem(Guid.Parse(guid), name, source, "Window", true, g, ug, light)
            {
                ApertureConstruction = new ApertureConstruction(Guid.Parse(guid), name, ApertureType.Window),
                MaterialLibrary = new global::SAM.Core.MaterialLibrary("Test"),
            };
        }

        /// <summary>One glazing source with one window system, so the session calculates it (the calculation is the stand-in).</summary>
        public static GlazingSource Source(string label = "My glazing systems")
        {
            ApertureConstruction apertureConstruction = new ApertureConstruction(Guid.NewGuid(), "Any", ApertureType.Window);
            return new GlazingSource(GlazingSourceKind.User, label, new ConstructionManager(new[] { apertureConstruction }, null, new global::SAM.Core.MaterialLibrary("Test")));
        }

        /// <summary>A session reading <paramref name="inventory"/> for any folder, and <see cref="Pool"/> for any glazing source.</summary>
        public static TasModelSession Session(TasModelInventory? inventory = null, List<string>? reads = null)
        {
            TasModelInventory value = inventory ?? Demo();
            return new TasModelSession(
                folder =>
                {
                    reads?.Add(folder);
                    return value;
                },
                source => Pool());
        }

        /// <summary>A session whose model read fails as it does without Tas.</summary>
        public static TasModelSession FailingSession()
        {
            return new TasModelSession(folder => throw new System.Runtime.InteropServices.COMException("Class not registered (Exception from HRESULT: 0x80040154)"), source => Pool());
        }

        /// <summary>A session already read (the model and the pool), for checks and forms without a window.</summary>
        public static async System.Threading.Tasks.Task<TasModelSession> ReadSession(string folder, TasModelInventory? inventory = null)
        {
            TasModelSession result = Session(inventory);
            await result.ReadAsync(folder);
            await result.AddGlazingSourcesAsync(new[] { Source() });
            return result;
        }

        /// <summary>The catalogue of the Demo with the pool (no Tas).</summary>
        public static OptimisationCatalogue Catalogue(TasGlazingFilter? filter = null)
        {
            return Analytical.Tas.GenOpt.Query.TasModelCatalogue(Demo(), Pool(), filter);
        }

        public static OptimisationCatalogueEntry Variable(OptimisationCatalogue catalogue, string name)
        {
            return catalogue.Variables.Single(x => x.Name == name);
        }

        public static OptimisationCatalogueEntry Output(OptimisationCatalogue catalogue, string name)
        {
            return catalogue.Outputs.Single(x => x.Name == name);
        }

        /// <summary>The SAM fixture folder in the sibling SAM checkout (SAM/SAM/SAM.Tests/Golden/Optimisation).</summary>
        public static string FixtureDirectory()
        {
            for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
            {
                string path = Path.Combine(directoryInfo.FullName, "SAM", "SAM", "SAM.Tests", "Golden", "Optimisation");
                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            throw new DirectoryNotFoundException("The sibling SAM checkout (SAM/SAM/SAM.Tests/Golden/Optimisation) was not found above " + AppContext.BaseDirectory + ".");
        }

        /// <summary>Sets the stub's spec for a generated script while it is alive (the stub reads it when Script.txt is C#).</summary>
        public sealed class StubSpec : IDisposable
        {
            public StubSpec(string json)
            {
                Environment.SetEnvironmentVariable(StubSpecVariable, json);
            }

            public void Dispose()
            {
                Environment.SetEnvironmentVariable(StubSpecVariable, null);
            }
        }
    }
}
