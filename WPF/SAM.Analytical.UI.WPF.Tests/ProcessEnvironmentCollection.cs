// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// The xUnit collection for classes that set process environment variables (SAM_COMMIT, say), which every other test and every
    /// child process would otherwise see. It never runs in parallel with anything else.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class ProcessEnvironmentCollection
    {
        public const string Name = "Process environment";
    }
}
