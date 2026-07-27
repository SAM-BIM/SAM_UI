// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;

namespace SAM.Core.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// Formats a duration with explicit units so the number can never be misread: seconds under a minute,
        /// then minutes and seconds, then hours, minutes and seconds. A colon-separated form was deliberately
        /// avoided because "12:30" reads as either mm:ss or hh:mm, and a TAS run can legitimately be either.
        /// <para>
        /// A deliberate twin of <c>SAM.Core.Windows.Query.Duration</c> rather than a shared implementation.
        /// This assembly holds no reference to SAM.Core.Windows — the WinForms stack is retired here — and the
        /// only home both could share is SAM.Core, which would put a formatting helper used by two progress
        /// dialogs into the core geometry/collection library and add a cross-repo build-order dependency for
        /// fifteen lines. If the wording ever changes, change it in both.
        /// </para>
        /// </summary>
        public static string Duration(TimeSpan timeSpan)
        {
            if (timeSpan.TotalSeconds < 1.0)
            {
                return "0s";
            }

            if (timeSpan.TotalMinutes < 1.0)
            {
                return string.Format("{0}s", (int)timeSpan.TotalSeconds);
            }

            if (timeSpan.TotalHours < 1.0)
            {
                return string.Format("{0}m {1:00}s", (int)timeSpan.TotalMinutes, timeSpan.Seconds);
            }

            return string.Format("{0}h {1:00}m {2:00}s", (int)timeSpan.TotalHours, timeSpan.Minutes, timeSpan.Seconds);
        }
    }
}
