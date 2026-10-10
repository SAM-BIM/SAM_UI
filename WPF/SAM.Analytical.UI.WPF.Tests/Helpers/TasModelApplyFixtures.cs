// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

#nullable enable

using SAM.Analytical.Tas.GenOpt;
using SAM.Analytical.UI.WPF.Tests.Helpers;
using SAM.Core;
using SAM.Core.Optimisation;
using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// "Apply best design" (PR9) without Tas: a small SAM model and the Tas model SAM_Tas would make of it (one TBD internal
    /// condition per space, named after it; the windows' pane as "Windows: GLZ -pane"), a pool system with its own frame,
    /// and a run of SAM_Tas' runner through the stub TasGenExecute, so the plan has a real best point and the hashes of
    /// the files the run evaluated.
    /// </summary>
    public static class TasModelApplyFixtures
    {
        public const string Studio1 = "Studio 1_0";
        public const string Studio2 = "Studio 2_0";
        public const string SharedCooling = "Cooling 23";
        public const string Studio1Heating = "Heating 21 - Studio 1_0";
        public const string Studio2Heating = "Heating 21 - Studio 2_0";
        public const string Tbd = "Model.tbd";
        public const string Tsd = "Model.tsd";
        public const string Tpd = "Model.tpd";
        public const string SystemFrame = "SystemFrame";

        public static readonly Guid TripleGuid = new Guid("aaaaaaaa-0000-4000-8000-000000a5191c");

        /// <summary>The 24 hours of the shared cooling profile: 23 °C from 08:00 to 19:00, "no cooling" (150) otherwise.</summary>
        public static double[] CoolingHours()
        {
            return Enumerable.Range(0, 24).Select(h => h >= 8 && h < 20 ? 23.0 : 150.0).ToArray();
        }

        /// <summary>
        /// Two studios sharing the cooling profile, each with its own heating value profile; four windows of "GLZ" (pane and
        /// frame) and one of another system.
        /// </summary>
        /// <param name="shareCooling">False: Studio 2_0 has a cooling profile of its own (same values), so Studio 1_0's is used once.</param>
        /// <param name="studio1">The name of the first studio (another name: the model is not the Tas model's).</param>
        /// <param name="cooling1">The hours of the "Cooling 23" profile (another value: the model is not the Tas model's).</param>
        /// <param name="glazing">The "GLZ" windows' construction (another one of the same name and Guid: the model changed since its Energy Simulation).</param>
        /// <param name="materials">The model's materials (another "Clear6": the model changed since its Energy Simulation).</param>
        /// <param name="glazingRooms">The studio of each "GLZ" window (0: Studio 1_0, 1: Studio 2_0); two in each by default.</param>
        public static AnalyticalModel Model(bool shareCooling = true, string studio1 = Studio1, double[]? cooling1 = null, ApertureConstruction? glazing = null, MaterialLibrary? materials = null, int[]? glazingRooms = null)
        {
            AdjacencyCluster adjacencyCluster = new AdjacencyCluster();
            List<Space> spaces = new List<Space>();
            foreach (string name in new[] { studio1, Studio2 })
            {
                InternalCondition internalCondition = new InternalCondition(name);
                internalCondition.SetProfileName(ProfileType.Cooling, shareCooling || name != Studio2 ? SharedCooling : SharedCooling + " - " + Studio2);
                internalCondition.SetProfileName(ProfileType.Heating, name == Studio2 ? Studio2Heating : Studio1Heating);
                Space space = new Space(name, new Point3D(name == Studio2 ? 10 : 0, 0, 1))
                {
                    InternalCondition = internalCondition,
                };

                adjacencyCluster.AddObject(space);
                spaces.Add(space);
            }

            // Each "GLZ" window's wall bounds one studio, as an external wall of an Energy Simulation model does.
            ApertureConstruction current = glazing ?? GlazingFixture.Current();
            int[] rooms = glazingRooms ?? GlazingRooms;
            for (int i = 0; i < rooms.Length; i++)
            {
                Panel panel = GlazingFixture.PanelWithWindow(current, i, out Aperture _);
                adjacencyCluster.AddObject(panel);
                adjacencyCluster.AddRelation(spaces[rooms[i]], panel);
            }

            adjacencyCluster.AddObject(GlazingFixture.PanelWithWindow(GlazingFixture.System(GlazingFixture.PaneOnlyGuid, "Other", ApertureType.Window, GlazingFixture.Clear, false), rooms.Length, out Aperture _));

            ProfileLibrary profileLibrary = new ProfileLibrary("Profiles");
            profileLibrary.Add(new Profile(SharedCooling, ProfileType.Cooling, cooling1 ?? CoolingHours()));
            if (!shareCooling)
            {
                profileLibrary.Add(new Profile(SharedCooling + " - " + Studio2, ProfileType.Cooling, CoolingHours()));
            }

            profileLibrary.Add(new Profile(Studio1Heating, ProfileType.Heating, new double[] { 21 }));
            profileLibrary.Add(new Profile(Studio2Heating, ProfileType.Heating, new double[] { 21 }));

            return new AnalyticalModel("Apply", null, null, null, adjacencyCluster, materials ?? GlazingFixture.ModelMaterials(), profileLibrary);
        }

        /// <summary>The studios of the four "GLZ" windows in <see cref="Model"/>: two in Studio 1_0, two in Studio 2_0.</summary>
        public static readonly int[] GlazingRooms = { 0, 0, 1, 1 };

        /// <summary>The TBD pane name SAM_Tas gives the model's "GLZ" windows.</summary>
        public static string Glazing => Query.TasPaneConstructionNames(GlazingFixture.Current())[0];

        /// <summary>
        /// "Windows: GLZ -pane" as the inventory reads it from the TBD an Energy Simulation of <see cref="Model"/> makes:
        /// the model's pane and frame layers (SAM_Tas' description of them) and a pane zone surface per window in its studio.
        /// </summary>
        public static TasGlazingConstructionInfo GlazingInfo(string? name = null, IEnumerable<string>? elements = null)
        {
            ApertureConstruction current = GlazingFixture.Current();
            MaterialLibrary materials = GlazingFixture.ModelMaterials();
            return new TasGlazingConstructionInfo(name ?? Glazing, elements ?? new[] { "W1", "W2" }, 0.6, 1.4, 0.78,
                Analytical.Tas.GenOpt.Query.TasMaterialLayers(current.PaneConstructionLayers, materials),
                new[] { Analytical.Tas.GenOpt.Query.TasMaterialLayers(current.FrameConstructionLayers, materials) },
                new Dictionary<string, int> { { Studio1, 2 }, { Studio2, 2 } });
        }

        /// <summary>The Tas model SAM_Tas makes of <see cref="Model"/>, with Studio 1_0's cooling hours, heating and the controller as given.</summary>
        public static TasModelInventory Inventory(double[]? cooling1 = null, double heating1 = 21, string? glazing = null, IEnumerable<string>? glazingElements = null, bool tpd = true, double controller = 3, TasGlazingConstructionInfo? glazingInfo = null)
        {
            float[] hours1 = (cooling1 ?? CoolingHours()).Select(x => (float)x).ToArray();
            float[] hours2 = CoolingHours().Select(x => (float)x).ToArray();
            return new TasModelInventory(
                Tbd,
                Tsd,
                tpd ? Tpd : null,
                new[]
                {
                    new TasInternalConditionInfo(Studio1, "Studio - " + Studio1, 1, new TasSetpointProfile(TasSetpointProfileType.Value, 1, (float)heating1), Hourly(hours1)),
                    new TasInternalConditionInfo(Studio1 + " - HDD", null, 1, new TasSetpointProfile(TasSetpointProfileType.Value, 1, 21), new TasSetpointProfile(TasSetpointProfileType.Value, 1, 150)),
                    new TasInternalConditionInfo(Studio2, "Studio - " + Studio2, 1, new TasSetpointProfile(TasSetpointProfileType.Value, 1, 21), Hourly(hours2)),
                },
                new[]
                {
                    glazingInfo ?? GlazingInfo(glazing, glazingElements),
                },
                tpd ? new[] { new TasPlantRoomInfo("Plant Room", new[] { new TasPlantControllerInfo("HeatPumpController", "tpdTempSensor", controller) }) } : null)
            {
                CoolingDemand = 2523.75187299657,
                HeatingDemand = 0,
            };
        }

        private static TasSetpointProfile Hourly(float[] hours)
        {
            float setpoint = hours.Min();
            return new TasSetpointProfile(TasSetpointProfileType.Hourly, 1, setpoint, hours.Count(x => x == setpoint), hours);
        }

        /// <summary>A window system with low-e panes and a frame of its own (not the model's), as a library system has.</summary>
        /// <param name="clear">The source's "Clear6" (another one: same name as the model's, other physics); the model's by default.</param>
        /// <param name="lowE">False: the source does not define the system's "LowE6".</param>
        public static TasGlazingSystem Triple(IMaterial? clear = null, bool lowE = true)
        {
            ApertureConstruction apertureConstruction = new ApertureConstruction(TripleGuid, "Triple low-e", ApertureType.Window,
                new List<ConstructionLayer> { new ConstructionLayer(GlazingFixture.LowE, 0.006), new ConstructionLayer(GlazingFixture.Argon, 0.012), new ConstructionLayer(GlazingFixture.Clear, 0.006) },
                new List<ConstructionLayer> { new ConstructionLayer(SystemFrame, 0.08) });
            apertureConstruction.SetValue(ApertureConstructionParameter.DefaultFrameWidth, 0.09);

            MaterialLibrary materialLibrary = new MaterialLibrary("My glazing systems");
            materialLibrary.Add(clear ?? GlazingFixture.ClearGlass());
            materialLibrary.Add(GlazingFixture.ArgonGas());
            materialLibrary.Add(GlazingFixture.FrameOpaque());
            if (lowE)
            {
                materialLibrary.Add(GlazingFixture.LowEGlass());
            }

            materialLibrary.Add(new OpaqueMaterial(Guid.NewGuid(), SystemFrame, SystemFrame, "System frame", 0.2, 1000, 700));

            return new TasGlazingSystem(TripleGuid, "Triple low-e", "My glazing systems", "Window", true, 0.36886733770370483, 0.997646152973175, 0.7281997203826904)
            {
                ApertureConstruction = apertureConstruction,
                MaterialLibrary = materialLibrary,
            };
        }

        public static string CoolingDefinition(double minimum = 21, double maximum = 28) => @"{
  ""schema"": ""sam.optimisation/1"",
  ""name"": ""Studio cooling"",
  ""model"": { ""engine"": ""tas-model"" },
  ""variables"": [
    { ""name"": ""Studio cooling"", ""quantity"": ""temperature"", ""unit"": ""°C"", ""minimum"": " + minimum.ToString(CultureInfo.InvariantCulture) + @", ""maximum"": " + maximum.ToString(CultureInfo.InvariantCulture) + @", ""start"": 23,
      ""target"": { ""kind"": ""tbd.internal-condition.cooling-setpoint"", ""reference"": { ""internalCondition"": ""Studio 1_0"" } } }
  ],
  ""outputs"": [
    { ""name"": ""Cooling demand"", ""unit"": ""kWh"", ""measure"": { ""kind"": ""tsd.annual-cooling-demand"" } }
  ],
  ""objective"": { ""output"": ""Cooling demand"", ""sense"": ""minimise"" },
  ""method"": { ""algorithm"": ""golden-section"", ""tolerance"": 0.5 }
}";

        public static string GlazingDefinition() => @"{
  ""schema"": ""sam.optimisation/1"",
  ""name"": ""Studio glazing"",
  ""model"": { ""engine"": ""tas-model"" },
  ""variables"": [
    { ""name"": ""Glazing"", ""type"": ""discrete"", ""minimum"": 1, ""maximum"": 2,
      ""target"": { ""kind"": ""tbd.glazing-construction.choice"", ""reference"": { ""glazingConstruction"": """ + Glazing + @""" },
                  ""options"": [ """ + Glazing + @""", ""Triple low-e"" ] } }
  ],
  ""outputs"": [
    { ""name"": ""Cooling demand"", ""unit"": ""kWh"", ""measure"": { ""kind"": ""tsd.annual-cooling-demand"" } }
  ],
  ""objective"": { ""output"": ""Cooling demand"", ""sense"": ""minimise"" },
  ""method"": { ""algorithm"": ""try-every-option"" }
}";

        public static string ControllerDefinition() => @"{
  ""schema"": ""sam.optimisation/1"",
  ""name"": ""Heat pump"",
  ""model"": { ""engine"": ""tas-model"" },
  ""variables"": [
    { ""name"": ""Heat pump setpoint"", ""minimum"": -5, ""maximum"": 35, ""start"": 3,
      ""target"": { ""kind"": ""tpd.controller.setpoint"", ""reference"": { ""plantRoom"": ""Plant Room"", ""controller"": ""HeatPumpController"" } } }
  ],
  ""outputs"": [
    { ""name"": ""Plant energy"", ""unit"": ""kWh"", ""measure"": { ""kind"": ""tpd.annual-energy"" } }
  ],
  ""objective"": { ""output"": ""Plant energy"", ""sense"": ""minimise"" },
  ""method"": { ""algorithm"": ""golden-section"", ""tolerance"": 0.5 }
}";

        public static OptimisationDefinition Read(string json)
        {
            OptimisationDefinition? result = global::SAM.Core.Optimisation.Create.OptimisationDefinition(json, out List<OptimisationDiagnostic> diagnostics);
            return result ?? throw new InvalidOperationException(string.Join("\n", diagnostics));
        }

        /// <summary>A project folder holding stand-in TBD, TSD and TPD files (only stand-in writers open them).</summary>
        public static string Project(string folder, bool tpd = true)
        {
            Directory.CreateDirectory(folder);
            System.IO.File.WriteAllText(Path.Combine(folder, Tbd), "tbd");
            System.IO.File.WriteAllText(Path.Combine(folder, Tsd), "tsd");
            if (tpd)
            {
                System.IO.File.WriteAllText(Path.Combine(folder, Tpd), "tpd");
            }

            return folder;
        }

        /// <summary>
        /// Runs <paramref name="json"/> through SAM_Tas' runner and the stub TasGenExecute (the stub's objective is a
        /// quadratic around <paramref name="center"/>), and returns the runner and the window's report of the run.
        /// </summary>
        public static TasOptimisationReport Run(string json, string project, TasModelInventory inventory, double center, out TasModelRunner runner, out OptimisationDefinition definition, TasGlazingSystem? triple = null)
        {
            definition = Read(json);
            TasModelRunSettings settings = new TasModelRunSettings(project, Path.Combine(project, "SAM_NativeGenOpt"), TasOptimisationWorkspace.StubExecutable)
            {
                Inventory = inventory,
                GlazingPool = new List<TasGlazingSystem> { triple ?? Triple() },
            };

            runner = new TasModelRunner(definition, settings) { GlazingWriter = (tbd, options) => { } };
            using (new TasModelJourneyFixtures.StubSpec("{\"kind\":\"quadratic\",\"center\":[" + center.ToString("R", CultureInfo.InvariantCulture) + "],\"offset\":10,\"outputs\":[\"Y1\"]}"))
            {
                NativeGenOptRun run = runner.Run();
                return new TasOptimisationReport(run, false, new TasOptimisationFormatter(definition));
            }
        }
    }
}
