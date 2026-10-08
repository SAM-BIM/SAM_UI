// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// One output of the TasGenExecute script, by the name the script writes (<c>ScriptOutput.SetValue("name", …)</c>):
    /// a SAM.Core.Optimisation OptimisationOutput (name, description, unit). Exactly one output is the primary one, the
    /// definition's objective; the others are recorded. SAM_Tas passes the objective first, as the native optimiser
    /// minimises the first output it is given.
    /// </summary>
    public sealed class TasOptimisationObjectiveRow : INotifyPropertyChanged
    {
        private string name = string.Empty;
        private string description = string.Empty;
        private string unit = string.Empty;
        private bool primary;

        public TasOptimisationObjectiveRow()
        {
        }

        public TasOptimisationObjectiveRow(string? name, bool primary, string? description = null, string? unit = null)
        {
            this.name = name ?? string.Empty;
            this.primary = primary;
            this.description = description ?? string.Empty;
            this.unit = unit ?? string.Empty;
        }

        public TasOptimisationObjectiveRow(TasOptimisationObjectiveRow tasOptimisationObjectiveRow)
            : this(tasOptimisationObjectiveRow?.name, tasOptimisationObjectiveRow?.primary ?? false, tasOptimisationObjectiveRow?.description, tasOptimisationObjectiveRow?.unit)
        {
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name
        {
            get => name;
            set => Set(ref name, value);
        }

        /// <summary>The output in engineering terms. Optional.</summary>
        public string Description
        {
            get => description;
            set => Set(ref description, value);
        }

        /// <summary>The unit as the definition declares it, for example "GBP". Optional; it describes the values and is not converted.</summary>
        public string Unit
        {
            get => unit;
            set => Set(ref unit, value);
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
