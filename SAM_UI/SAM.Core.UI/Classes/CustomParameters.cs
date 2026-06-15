// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections;

namespace SAM.Core.UI
{
    /// <summary>
    /// Ordered collection of <see cref="CustomParameter"/>. Ported from
    /// SAM.Core.Windows.CustomParameters minus the WinForms PropertyGrid plumbing
    /// (ICustomTypeDescriptor / PropertyDescriptor) which the WPF ParametersControl
    /// does not need — it binds to the collection directly.
    /// </summary>
    public class CustomParameters : CollectionBase
    {
        public void Add(CustomParameter customParameter)
        {
            List.Add(customParameter);
        }

        public void Remove(string name)
        {
            foreach (CustomParameter customParameter in List)
            {
                if (customParameter?.Name == name)
                {
                    List.Remove(customParameter);
                    return;
                }
            }
        }

        public CustomParameter this[int index]
        {
            get
            {
                return (CustomParameter)List[index];
            }
            set
            {
                List[index] = value;
            }
        }
    }
}
