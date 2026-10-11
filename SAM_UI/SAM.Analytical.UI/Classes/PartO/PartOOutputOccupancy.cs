// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// What a Part O case folder already holds, for one prepared run, read without changing anything
    /// (<see cref="PartOOutputPaths.Occupancy"/>). It is the same question <see cref="PartOOutputPaths.TryClaimRun"/>
    /// asks before it refuses - answered up front, so a person can be asked rather than refused.
    /// </summary>
    public sealed class PartOOutputOccupancy
    {
        public PartOOutputOccupancy(string directory_Case, int count_GeneratedFiles, int count_OtherFiles, DateTime? lastWriteUtc, bool ownedByRun)
        {
            Directory_Case = directory_Case;
            Count_GeneratedFiles = count_GeneratedFiles;
            Count_OtherFiles = count_OtherFiles;
            LastWriteUtc = lastWriteUtc;
            IsOwnedByRun = ownedByRun;
        }

        /// <summary>The case folder, as it would be written to.</summary>
        public string Directory_Case { get; }

        /// <summary>Files in the case's own <c>tas</c>, <c>reports</c> and <c>diagnostics</c> folders - what a replacement removes.</summary>
        public int Count_GeneratedFiles { get; }

        /// <summary>Any other file in the case folder (not the case marker) - never removed by a replacement.</summary>
        public int Count_OtherFiles { get; }

        /// <summary>When something in the folder was last written (UTC), or null where nothing is there.</summary>
        public DateTime? LastWriteUtc { get; }

        /// <summary>Whether the folder's evidence belongs to the run (and TAS case) asking: a retry by the same run is never refused.</summary>
        public bool IsOwnedByRun { get; }

        /// <summary>Whether the folder holds anything beyond SAM's own case marker.</summary>
        public bool IsOccupied => Count_GeneratedFiles + Count_OtherFiles != 0;

        /// <summary>Whether a run of the asking kind would be refused: there is evidence, and it is another run's.</summary>
        public bool NeedsReplacement => IsOccupied && !IsOwnedByRun;
    }
}
