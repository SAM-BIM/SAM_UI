// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace SAM.Core.UI
{
    /// <summary>
    /// Lightweight, opt-in timing log used to measure UI operations on large models
    /// (view regeneration, model cloning, scene rebuilds — see issues #11-#16).
    ///
    /// Disabled unless the SAM_UI_PERFORMANCE_LOG environment variable is set:
    ///   SAM_UI_PERFORMANCE_LOG=1            -> log to %TEMP%\SAM_UI_Performance.log
    ///   SAM_UI_PERFORMANCE_LOG=C:\dir\x.log -> log to the given file
    /// Lines are tab-separated: timestamp, elapsed [ms], operation, detail.
    /// Entries are also written to the debugger output window.
    /// </summary>
    public static class PerformanceLog
    {
        private static readonly object lockObject = new object();
        private static bool? enabled = null;
        private static string path = null;

        public static bool Enabled
        {
            get
            {
                lock (lockObject)
                {
                    if (enabled == null)
                    {
                        string value = Environment.GetEnvironmentVariable("SAM_UI_PERFORMANCE_LOG");
                        if (string.IsNullOrWhiteSpace(value) || value.Trim() == "0")
                        {
                            enabled = false;
                        }
                        else
                        {
                            value = value.Trim();
                            path = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                                ? Path.Combine(Path.GetTempPath(), "SAM_UI_Performance.log")
                                : value;
                            enabled = true;
                        }
                    }

                    return enabled.Value;
                }
            }
        }

        /// <summary>
        /// Starts measuring an operation; disposing the result writes the log entry.
        /// Returns null when logging is disabled (safe to use directly in a using statement).
        /// When minMilliseconds is set, the entry is only written if the operation took at least that long
        /// (use for high-frequency operations such as hit-testing on mouse move).
        /// </summary>
        public static IDisposable Measure(string operation, string detail = null, double minMilliseconds = 0)
        {
            if (!Enabled)
            {
                return null;
            }

            return new Measurement(operation, detail, minMilliseconds);
        }

        public static void Write(string operation, string detail, double milliseconds)
        {
            if (!Enabled)
            {
                return;
            }

            string line = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd HH:mm:ss.fff}\t{1,10:F1}\t{2}\t{3}",
                DateTime.Now, milliseconds, operation, detail ?? string.Empty);

            Debug.WriteLine("[SAM PerformanceLog] " + line);

            lock (lockObject)
            {
                try
                {
                    File.AppendAllText(path, line + Environment.NewLine);
                }
                catch (Exception)
                {
                    // Logging must never break the UI; ignore IO errors (locked/unwritable file etc.)
                }
            }
        }

        private class Measurement : IDisposable
        {
            private readonly string operation;
            private readonly string detail;
            private readonly double minMilliseconds;
            private readonly Stopwatch stopwatch;

            public Measurement(string operation, string detail, double minMilliseconds)
            {
                this.operation = operation;
                this.detail = detail;
                this.minMilliseconds = minMilliseconds;
                stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                stopwatch.Stop();

                double milliseconds = stopwatch.Elapsed.TotalMilliseconds;
                if (milliseconds >= minMilliseconds)
                {
                    Write(operation, detail, milliseconds);
                }
            }
        }
    }
}
