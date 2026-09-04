// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Windows.Media;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// One row of the Prepare and Run status list, as the view binds it.
    /// <para>
    /// <b>Presentation only.</b> Every value is read off the <see cref="PartOWorkflowStageState"/> it wraps;
    /// the only thing added is the colour, and even that is a function of the status the inspection assigned.
    /// No row can say anything the inspection did not.
    /// </para>
    /// </summary>
    public class PartOWorkflowStatusRow
    {
        public PartOWorkflowStatusRow(PartOWorkflowStageState partOWorkflowStageState)
        {
            State = partOWorkflowStageState;
        }

        public PartOWorkflowStageState State { get; }

        public string Name => State.Name;

        public string StatusText => State.StatusText;

        public string Detail => State.Detail;

        /// <summary>
        /// The status colour. Red is reserved for the one status that stops Run, so a person scanning the
        /// list is never alarmed by a stage that is merely going to be built.
        /// </summary>
        public Brush Foreground
        {
            get
            {
                return State.Status switch
                {
                    PartOWorkflowStageStatus.Blocked => Brushes.Firebrick,
                    PartOWorkflowStageStatus.Ready => Brushes.DarkGreen,
                    PartOWorkflowStageStatus.Reused => Brushes.DarkGreen,
                    PartOWorkflowStageStatus.Prepare => Brushes.DarkGoldenrod,
                    _ => Brushes.DimGray,
                };
            }
        }

        public override string ToString()
        {
            return State.ToString();
        }
    }
}
