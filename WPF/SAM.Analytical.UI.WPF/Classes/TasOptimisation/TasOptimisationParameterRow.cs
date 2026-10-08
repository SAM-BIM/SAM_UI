// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// One design variable as typed in Simulate &gt; Optimisation. The values are kept as text, exactly as entered, and
    /// are read by <see cref="TasOptimisationInput.TryGetDefinition"/> into a SAM.Core.Optimisation DesignVariable
    /// (name, description, unit, start, minimum, maximum, step), which SAM_Tas runs as a NumberParameter.
    /// </summary>
    public sealed class TasOptimisationParameterRow : INotifyPropertyChanged
    {
        private string name = string.Empty;
        private string description = string.Empty;
        private string unit = string.Empty;
        private string start = string.Empty;
        private string minimum = string.Empty;
        private string maximum = string.Empty;
        private string step = string.Empty;
        private bool startAndStepApplicable = true;

        public TasOptimisationParameterRow()
        {
        }

        public TasOptimisationParameterRow(string? name, string? start, string? minimum, string? maximum, string? step, string? description = null, string? unit = null)
        {
            this.name = name ?? string.Empty;
            this.description = description ?? string.Empty;
            this.unit = unit ?? string.Empty;
            this.start = start ?? string.Empty;
            this.minimum = minimum ?? string.Empty;
            this.maximum = maximum ?? string.Empty;
            this.step = step ?? string.Empty;
        }

        public TasOptimisationParameterRow(TasOptimisationParameterRow tasOptimisationParameterRow)
            : this(tasOptimisationParameterRow?.name, tasOptimisationParameterRow?.start, tasOptimisationParameterRow?.minimum, tasOptimisationParameterRow?.maximum, tasOptimisationParameterRow?.step, tasOptimisationParameterRow?.description, tasOptimisationParameterRow?.unit)
        {
            startAndStepApplicable = tasOptimisationParameterRow?.startAndStepApplicable ?? true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name
        {
            get => name;
            set => Set(ref name, value);
        }

        /// <summary>The design variable in engineering terms. Optional.</summary>
        public string Description
        {
            get => description;
            set => Set(ref description, value);
        }

        /// <summary>The unit as the definition declares it, for example "°C". Optional; it describes the values and is not converted.</summary>
        public string Unit
        {
            get => unit;
            set => Set(ref unit, value);
        }

        /// <summary>The start value (GenOpt Ini). Not used by golden section.</summary>
        public string Start
        {
            get => start;
            set => Set(ref start, value);
        }

        public string Minimum
        {
            get => minimum;
            set => Set(ref minimum, value);
        }

        public string Maximum
        {
            get => maximum;
            set => Set(ref maximum, value);
        }

        /// <summary>The pattern-search step. Not used by golden section.</summary>
        public string Step
        {
            get => step;
            set => Set(ref step, value);
        }

        /// <summary>
        /// False for golden section, which reads only the bounds: Start and Step are then shown as not applicable,
        /// kept as they are, and excluded from validation.
        /// </summary>
        public bool StartAndStepApplicable
        {
            get => startAndStepApplicable;
            set
            {
                if (Set(ref startAndStepApplicable, value))
                {
                    OnPropertyChanged(nameof(StartAndStepNotApplicable));
                }
            }
        }

        public bool StartAndStepNotApplicable => !startAndStepApplicable;

        private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string? propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
