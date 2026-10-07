// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// One output of the TasGenExecute script, by the name the script writes (<c>ScriptOutput.SetValue("name", …)</c>).
    /// Exactly one output is the primary objective: the native optimiser minimises the FIRST objective it is given,
    /// so the primary one is placed first and the others follow, recorded only.
    /// </summary>
    public sealed class TasOptimisationObjectiveRow : INotifyPropertyChanged
    {
        private string name = string.Empty;
        private bool primary;

        public TasOptimisationObjectiveRow()
        {
        }

        public TasOptimisationObjectiveRow(string? name, bool primary)
        {
            this.name = name ?? string.Empty;
            this.primary = primary;
        }

        public TasOptimisationObjectiveRow(TasOptimisationObjectiveRow tasOptimisationObjectiveRow)
            : this(tasOptimisationObjectiveRow?.name, tasOptimisationObjectiveRow?.primary ?? false)
        {
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name
        {
            get => name;
            set => Set(ref name, value);
        }

        /// <summary>True for the one output that is minimised.</summary>
        public bool Primary
        {
            get => primary;
            set => Set(ref primary, value);
        }

        private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
