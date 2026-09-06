// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using System;
using System.Text.Json.Nodes;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// How one saved view presents the Ventilation Design floor-plan overlay.
    /// <para>
    /// <b>Presentation only</b>, matching <see cref="PartFAirflowViewSettings"/>. Not one design flow rate is
    /// stored here - every value is read live from <c>VentilationTerminal.DesignFlowRate_Lps</c> through
    /// <c>Query.VentilationTerminalDesignDuty_Lps</c> at draw time, so an Iteration 2B round that raises a
    /// room's design airflow is reflected the next time the view redraws, with nothing here to go stale.
    /// </para>
    /// <para>
    /// Attached to a view through <see cref="AnalyticalViewSettingsParameter.VentilationDesignAirflow"/>.
    /// Absent means the overlay is off, so a view saved before this existed reopens exactly as it was.
    /// </para>
    /// </summary>
    public class DesignAirFlowViewSettings : SAMObject
    {
        public DesignAirFlowViewSettings()
            : base(Guid.NewGuid(), "Ventilation Design")
        {
        }

        public DesignAirFlowViewSettings(JsonObject jsonObject)
            : base(jsonObject)
        {
        }

        public DesignAirFlowViewSettings(DesignAirFlowViewSettings designAirFlowViewSettings)
            : base(designAirFlowViewSettings)
        {
            if (designAirFlowViewSettings is not null)
            {
                Enabled = designAirFlowViewSettings.Enabled;
                ShowSupply = designAirFlowViewSettings.ShowSupply;
                ShowExtract = designAirFlowViewSettings.ShowExtract;
                ShowNet = designAirFlowViewSettings.ShowNet;
            }
        }

        /// <summary>Whether this view wants the Ventilation Design overlay. False by default.</summary>
        public bool Enabled { get; set; }

        public bool ShowSupply { get; set; } = true;

        public bool ShowExtract { get; set; } = true;

        /// <summary>
        /// Whether a space's net (supply minus extract) is also drawn, where both directions are established.
        /// Off by default: the net is a derived reading, not the design duty itself, and should be opted
        /// into rather than shown alongside it unasked.
        /// </summary>
        public bool ShowNet { get; set; }

        public override bool FromJsonObject(JsonObject jsonObject)
        {
            if (!base.FromJsonObject(jsonObject))
            {
                return false;
            }

            Enabled = PartFViewJson.Boolean(jsonObject, "Enabled", Enabled);
            ShowSupply = PartFViewJson.Boolean(jsonObject, "ShowSupply", ShowSupply);
            ShowExtract = PartFViewJson.Boolean(jsonObject, "ShowExtract", ShowExtract);
            ShowNet = PartFViewJson.Boolean(jsonObject, "ShowNet", ShowNet);

            return true;
        }

        public override JsonObject ToJsonObject()
        {
            JsonObject result = base.ToJsonObject();
            if (result is null)
            {
                return result;
            }

            result["Enabled"] = Enabled;
            result["ShowSupply"] = ShowSupply;
            result["ShowExtract"] = ShowExtract;
            result["ShowNet"] = ShowNet;

            return result;
        }
    }
}
