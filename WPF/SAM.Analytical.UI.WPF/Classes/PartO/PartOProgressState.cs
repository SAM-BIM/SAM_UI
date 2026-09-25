// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Globalization;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>Where one stage of a long Part O operation is.</summary>
    public enum PartOProgressStageStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Skipped,
    }

    /// <summary>
    /// The stage list of one long Part O operation - what finished, what is running, and for how long.
    ///
    /// <para><b>Honest by construction</b></para>
    /// <para>
    /// There is no percentage and no day counter here, because TAS reports neither: its interop exposes no
    /// simulation events and nothing in SAM polls for a day. What is known is which stage has started, which
    /// have finished, how long each took and, where the workflow announces them, the name of the step inside
    /// a stage. That is exactly what this holds.
    /// </para>
    /// <para>
    /// <b>Pure.</b> No window, no thread, and the clock is injected - so the transitions a person sees are
    /// testable without a desktop. <see cref="PartOProgressHost"/> renders it.
    /// </para>
    /// </summary>
    public class PartOProgressState
    {
        private readonly Func<DateTime> clock;

        private readonly object @lock = new();

        private readonly List<string> names = [];

        private readonly List<PartOProgressStageStatus> statuses = [];

        private readonly List<DateTime?> starts = [];

        private readonly List<TimeSpan?> durations = [];

        private readonly DateTime start;

        private DateTime? end;

        private string detail;

        public PartOProgressState(IEnumerable<string> stageNames, Func<DateTime> clock = null)
        {
            this.clock = clock ?? (() => DateTime.UtcNow);

            foreach (string name in stageNames ?? [])
            {
                names.Add(name);
                statuses.Add(PartOProgressStageStatus.Pending);
                starts.Add(null);
                durations.Add(null);
            }

            start = this.clock();
        }

        public int Count => names.Count;

        /// <summary>
        /// Starts a stage. Every earlier stage still running is completed, and every earlier stage that never
        /// started is marked skipped - so the list never shows two stages running, or a later stage done while
        /// an earlier one still reads as pending.
        /// </summary>
        public void Start(int index)
        {
            lock (@lock)
            {
                if (index < 0 || index >= names.Count || statuses[index] == PartOProgressStageStatus.Running)
                {
                    return;
                }

                DateTime now = clock();

                for (int i = 0; i < names.Count; i++)
                {
                    if (i == index)
                    {
                        continue;
                    }

                    if (statuses[i] == PartOProgressStageStatus.Running)
                    {
                        Finish(i, PartOProgressStageStatus.Completed, now);
                    }
                    else if (i < index && statuses[i] == PartOProgressStageStatus.Pending)
                    {
                        statuses[i] = PartOProgressStageStatus.Skipped;
                    }
                }

                statuses[index] = PartOProgressStageStatus.Running;
                starts[index] = now;
                durations[index] = null;
                detail = null;
            }
        }

        /// <summary>The step inside the running stage, where the workflow names one. Null clears it.</summary>
        public string Detail
        {
            get
            {
                lock (@lock)
                {
                    return detail;
                }
            }

            set
            {
                lock (@lock)
                {
                    detail = value;
                }
            }
        }

        /// <summary>Completes the running stage, and ends the operation's clock where it is the last word.</summary>
        public void Complete(bool final = true)
        {
            lock (@lock)
            {
                DateTime now = clock();

                for (int i = 0; i < names.Count; i++)
                {
                    if (statuses[i] == PartOProgressStageStatus.Running)
                    {
                        Finish(i, PartOProgressStageStatus.Completed, now);
                    }
                }

                detail = null;

                if (final)
                {
                    end = now;
                }
            }
        }

        /// <summary>Fails the running stage (or, where none is running, the first pending one) and ends the clock.</summary>
        public void Fail(string text = null)
        {
            lock (@lock)
            {
                DateTime now = clock();

                int index = statuses.IndexOf(PartOProgressStageStatus.Running);

                if (index < 0)
                {
                    index = statuses.IndexOf(PartOProgressStageStatus.Pending);
                }

                if (index >= 0)
                {
                    Finish(index, PartOProgressStageStatus.Failed, now);
                }

                detail = text;
                end = now;
            }
        }

        /// <summary>How long the whole operation has run, or ran.</summary>
        public TimeSpan Elapsed
        {
            get
            {
                lock (@lock)
                {
                    return (end ?? clock()) - start;
                }
            }
        }

        public PartOProgressStageStatus Status(int index)
        {
            lock (@lock)
            {
                return index >= 0 && index < statuses.Count ? statuses[index] : PartOProgressStageStatus.Pending;
            }
        }

        public string Name(int index)
        {
            return index >= 0 && index < names.Count ? names[index] : null;
        }

        /// <summary>How long a stage took, or has taken so far while it runs. Null where it never started.</summary>
        public TimeSpan? Duration(int index)
        {
            lock (@lock)
            {
                if (index < 0 || index >= names.Count)
                {
                    return null;
                }

                if (statuses[index] == PartOProgressStageStatus.Running && starts[index].HasValue)
                {
                    return clock() - starts[index].Value;
                }

                return durations[index];
            }
        }

        /// <summary>One line per stage, as it is shown: "✓ TAS simulation · 7m 48s".</summary>
        public List<string> Lines()
        {
            List<string> result = [];

            for (int i = 0; i < names.Count; i++)
            {
                TimeSpan? duration = Duration(i);

                result.Add(string.Format(
                    "{0} {1}{2}",
                    Glyph(Status(i)),
                    names[i],
                    duration.HasValue && Status(i) != PartOProgressStageStatus.Pending ? " · " + Format(duration.Value) : string.Empty));
            }

            return result;
        }

        public static string Glyph(PartOProgressStageStatus partOProgressStageStatus)
        {
            return partOProgressStageStatus switch
            {
                PartOProgressStageStatus.Running => "●",
                PartOProgressStageStatus.Completed => "✓",
                PartOProgressStageStatus.Failed => "✕",
                PartOProgressStageStatus.Skipped => "–",
                _ => "○",
            };
        }

        /// <summary>"7m 48s", "42s", "1h 03m" - how a duration is written everywhere in the Part O workflow.</summary>
        public static string Format(TimeSpan timeSpan)
        {
            if (timeSpan < TimeSpan.Zero)
            {
                timeSpan = TimeSpan.Zero;
            }

            if (timeSpan.TotalHours >= 1)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}h {1:00}m", (int)timeSpan.TotalHours, timeSpan.Minutes);
            }

            if (timeSpan.TotalMinutes >= 1)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}m {1:00}s", (int)timeSpan.TotalMinutes, timeSpan.Seconds);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}s", (int)Math.Round(timeSpan.TotalSeconds));
        }

        private void Finish(int index, PartOProgressStageStatus partOProgressStageStatus, DateTime now)
        {
            statuses[index] = partOProgressStageStatus;
            durations[index] = starts[index].HasValue ? now - starts[index].Value : null;
        }
    }
}
