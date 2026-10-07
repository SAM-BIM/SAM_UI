// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.UI.WPF
{
    /// <summary>How one readiness check of Simulate &gt; Optimisation stands.</summary>
    public enum TasOptimisationCheckStatus
    {
        Ready,

        /// <summary>Worth knowing, but the run may start.</summary>
        Warning,

        /// <summary>The run cannot start until this is resolved.</summary>
        Blocked,
    }

    /// <summary>One line of the Optimisation window's readiness list: what is checked, how it stands, and why.</summary>
    public sealed class TasOptimisationCheck
    {
        public TasOptimisationCheck(TasOptimisationCheckStatus status, string title, string detail)
        {
            Status = status;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public TasOptimisationCheckStatus Status { get; }

        public string Title { get; }

        public string Detail { get; }

        /// <summary>The state as a glyph, so it never depends on colour alone.</summary>
        public string Glyph => Status switch
        {
            TasOptimisationCheckStatus.Ready => "✓",
            TasOptimisationCheckStatus.Warning => "⚠",
            _ => "✕",
        };

        public override string ToString()
        {
            return string.Format("{0} {1}: {2}", Glyph, Title, Detail);
        }
    }
}
