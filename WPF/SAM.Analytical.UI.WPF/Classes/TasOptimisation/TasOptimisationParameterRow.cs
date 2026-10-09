// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// One design variable as typed in Simulate &gt; Optimisation. The values are kept as text, exactly as entered, and
    /// are read by <see cref="TasOptimisationInput.TryGetDefinition"/> into a SAM.Core.Optimisation DesignVariable
    /// (name, description, unit, start, minimum, maximum, step), which SAM_Tas runs as a NumberParameter.
    /// <para>
    /// A row of the "tas-model" engine also says what it changes in the model (<see cref="Target"/>, from the model's
    /// catalogue or a definition) and, for a choice, that it is <c>"discrete"</c> (<see cref="Type"/>). Those are the
    /// row's own: renaming the row keeps them.
    /// </para>
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
            Target = tasOptimisationParameterRow?.Target == null ? null : new OptimisationTarget(tasOptimisationParameterRow.Target);
            Type = tasOptimisationParameterRow?.Type;
            Quantity = tasOptimisationParameterRow?.Quantity;
            QuantityUnit = tasOptimisationParameterRow?.QuantityUnit;
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

        /// <summary>What the variable changes in the model ("tas-model"); null for a variable a Tas script reads by name.</summary>
        public OptimisationTarget? Target { get; set; }

        /// <summary>The variable type; null for the default (continuous, or the loaded definition's type of the same name).</summary>
        public DesignVariableType? Type { get; set; }

        /// <summary>The declared quantity; null for the loaded definition's of the same name. It holds while the unit is <see cref="QuantityUnit"/>.</summary>
        public OptimisationQuantity? Quantity { get; set; }

        /// <summary>The unit <see cref="Quantity"/> was declared with: a different unit in the form drops the quantity.</summary>
        public string? QuantityUnit { get; set; }

        /// <summary>True for a choice between options (a <c>"discrete"</c> variable, numbered 1 to the number of options).</summary>
        public bool IsChoice => Type == DesignVariableType.Discrete;

        /// <summary>
        /// What the variable changes, in words, for the line under the row: "Changes: Zone heating setpoint of internal
        /// condition “Office”" or, for a choice, its numbered options. Empty for a script variable.
        /// </summary>
        public string BindingText
        {
            get
            {
                if (Target == null)
                {
                    return string.Empty;
                }

                string text = "Changes: " + Query.TasOptimisationBindingText(Target);
                if (Target.Options != null && Target.Options.Count != 0)
                {
                    text += ". Options: " + string.Join("; ", Target.Options.Select((x, i) => (i + 1).ToString(CultureInfo.InvariantCulture) + " " + x));
                }

                return text;
            }
        }

        public bool HasBinding => Target != null;

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
