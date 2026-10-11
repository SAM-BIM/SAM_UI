// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.GenOpt;
using SAM.Core.Optimisation;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// One line of the Design Optimisation window's "Can change" or "Can measure" list ("tas-model" engine): an item of
    /// the model's catalogue (SAM_Tas <c>Query.TasModelCatalogue</c>) with its current value, unit and suggested range, and
    /// for a glazing choice its options (g, Ug, light, source). Picking it (Add) adds a bound design variable or output to
    /// the form. For a measure with parameters (the overheating threshold) the value to add it with is editable.
    /// </summary>
    public sealed class TasModelCatalogueItem : INotifyPropertyChanged
    {
        private string parameterText;

        /// <param name="optimisationCatalogueEntry">The catalogue item.</param>
        /// <param name="glazingOptions">The glazing options when the item is a glazing choice (their summaries are shown).</param>
        public TasModelCatalogueItem(OptimisationCatalogueEntry optimisationCatalogueEntry, IEnumerable<TasGlazingOption>? glazingOptions = null)
        {
            Entry = optimisationCatalogueEntry;
            GlazingOptions = (glazingOptions ?? Enumerable.Empty<TasGlazingOption>()).ToList().AsReadOnly();

            OptimisationBindingParameter? parameter = Parameter;
            double? value = parameter == null ? null : (Entry.Measure?.Parameters != null && Entry.Measure.Parameters.TryGetValue(parameter.Name, out double current) ? current : parameter.Default);
            parameterText = value == null ? string.Empty : TasOptimisationInput.Text(value.Value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public OptimisationCatalogueEntry Entry { get; }

        public string Name => Entry.Name;

        public bool IsTarget => Entry.Target != null;

        public bool IsChoice => Entry.Options != null && Entry.Options.Count != 0;

        /// <summary>
        /// The value line: "now 21 °C; suggested 16 to 24 °C", "now 15,227.8 kWh", "3 options", "no suggested range: enter
        /// one". Numbers are shown at full precision (they are the values the form is filled with).
        /// </summary>
        public string Detail
        {
            get
            {
                List<string> parts = new List<string>();
                if (IsChoice)
                {
                    parts.Add(Entry.Options.Count.ToString(CultureInfo.InvariantCulture) + " options; option 1 is the model's glazing");
                    return string.Join("; ", parts);
                }

                if (Entry.Value != null)
                {
                    //A setpoint is shown exactly (it is the value the form starts from); a result as the results are (PR5b).
                    parts.Add("now " + (IsTarget ? WithUnit(Entry.Value.Value) : new TasOptimisationFormatter(new OptimisationDefinition()).Text(Entry.Value.Value, Entry.Unit)));
                }
                else if (!IsTarget)
                {
                    parts.Add("no stored result yet");
                }

                if (IsTarget)
                {
                    parts.Add(Entry.Minimum != null && Entry.Maximum != null
                        ? "suggested " + TasOptimisationInput.Text(Entry.Minimum.Value) + " to " + WithUnit(Entry.Maximum.Value)
                        : "no suggested range: enter one");
                }
                else if (Entry.Value == null && !string.IsNullOrWhiteSpace(Entry.Unit))
                {
                    parts.Add("in " + Entry.Unit);
                }

                return string.Join("; ", parts);
            }
        }

        public string Description => Entry.Description ?? string.Empty;

        /// <summary>The glazing options in g order, as the definition will number them.</summary>
        public IReadOnlyList<TasGlazingOption> GlazingOptions { get; }

        /// <summary>"1 Suncool Example: g 0.792, Ug 1.20, light 0.800 (Model (current))" per option, one per line.</summary>
        public string OptionsText => string.Join("\n", GlazingOptions.Select(x => x.Summary));

        public bool HasOptionsText => GlazingOptions.Count != 0;

        /// <summary>The measure's parameter offered for editing (the overheating threshold); null when there is none.</summary>
        public OptimisationBindingParameter? Parameter
        {
            get
            {
                if (Entry.Measure == null)
                {
                    return null;
                }

                OptimisationBindingCapability? kind = Analytical.Tas.GenOpt.Query.TasModelCapabilities().Measures?.FirstOrDefault(x => x?.Kind == Entry.Measure.Kind);
                return kind?.Parameters?.FirstOrDefault(x => x != null);
            }
        }

        public bool HasParameter => Parameter != null;

        /// <summary>"Threshold (°C)".</summary>
        public string ParameterLabel
        {
            get
            {
                OptimisationBindingParameter? parameter = Parameter;
                if (parameter == null)
                {
                    return string.Empty;
                }

                string name = parameter.Name.Length == 0 ? parameter.Name : char.ToUpperInvariant(parameter.Name[0]) + parameter.Name.Substring(1);
                return string.IsNullOrWhiteSpace(parameter.Unit) ? name : name + " (" + parameter.Unit + ")";
            }
        }

        /// <summary>The parameter value to add the measure with, as typed.</summary>
        public string ParameterText
        {
            get => parameterText;
            set
            {
                if (parameterText == value)
                {
                    return;
                }

                parameterText = value ?? string.Empty;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ParameterText)));
            }
        }

        /// <summary>
        /// The parameters to add the measure with: the typed value when it is a number, otherwise none (the engine's
        /// default). A value outside the kind's range is kept, and the definition's OPT605 says so.
        /// </summary>
        public Dictionary<string, double>? Parameters()
        {
            OptimisationBindingParameter? parameter = Parameter;
            if (parameter == null || !TasOptimisationInput.TryNumber(parameterText, out double value))
            {
                return null;
            }

            return new Dictionary<string, double> { { parameter.Name, value } };
        }

        private string WithUnit(double value)
        {
            string text = TasOptimisationInput.Text(value);
            return string.IsNullOrWhiteSpace(Entry.Unit) ? text : text + " " + Entry.Unit;
        }
    }
}
