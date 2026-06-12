// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace SAM.Core.UI
{
    public class UIJSAMObject<T> where T: IJSAMObject
    {
        private string path;

        protected T jSAMObject;

        // Optional cached deep clone for the JSAMObject getter. The getter clones the whole object on
        // every read (dozens of times per view reload). Subclasses whose reads are strictly read-only
        // can opt in via CacheJSAMObjectClone to collapse that to one clone per modification.
        // It is OFF by default: the default getter keeps its defensive-copy contract (a fresh, isolated
        // clone per read), which callers that hand sub-objects to modal editors and cancel rely on.
        // Cache is invalidated (InvalidateClone) whenever jSAMObject is replaced.
        private T cachedClone;
        private bool cachedCloneValid;

        // Opt in (override => true) only when every consumer treats the returned object as read-only.
        protected virtual bool CacheJSAMObjectClone => false;

        protected bool modified;

        public event EventHandler Opening;
        public event OpenedEventHandler Opened;

        public event EventHandler Saving;
        public event EventHandler Saved;

        public event EventHandler Closing;
        public event ClosedEventHandler Closed;

        public event ModifiedEventHandler Modified;

        public UIJSAMObject(string path)
        {
            this.path = path;
            modified = false;
        }

        public UIJSAMObject(T jSAMObject)
        {
            this.jSAMObject = jSAMObject;
            modified = false;
        }

        public UIJSAMObject()
        {
            
        }


        public string Path
        {
            get
            {
                return path;
            }
            set
            {
                path = value;
                modified = true;
            }
        }

        public T JSAMObject
        {
            get
            {
                if(jSAMObject == null)
                {
                    return default;
                }

                if (CacheJSAMObjectClone && cachedCloneValid)
                {
                    return cachedClone;
                }

                T clone;
                using (PerformanceLog.Measure("UIJSAMObject.Clone", typeof(T).Name))
                {
                    clone = Core.Query.Clone(jSAMObject);
                }

                if (CacheJSAMObjectClone)
                {
                    cachedClone = clone;
                    cachedCloneValid = true;
                }

                return clone;
            }

            set
            {
                SetJSAMObject(value, new FullModification());
            }
        }

        public void SetJSAMObject(T jSAMObject, IModification modification)
        {
            if(modification == null)
            {
                modification = new FullModification();
            }

            SetJSAMObject(jSAMObject, new List<IModification>() { modification });
        }

        public void SetJSAMObject(T jSAMObject, IEnumerable<IModification> modifications)
        {
            this.jSAMObject = jSAMObject;
            InvalidateClone();
            modified = true;
            OnModified(modifications);
        }

        // Subclasses that assign the jSAMObject field directly (e.g. via Load) must call this so the
        // cached clone returned by the JSAMObject getter does not go stale.
        protected void InvalidateClone()
        {
            cachedClone = default;
            cachedCloneValid = false;
        }


        public virtual bool Open()
        {
            OnOpening();

            bool result = false;
            if(!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                List<T> jSAMObjects = null;
                try
                {
                    jSAMObjects = Core.Convert.ToSAM<T>(path);
                }
                catch(Exception exception)
                {
                    return false;
                }

                if(jSAMObjects != null && jSAMObjects.Count != 0)
                {
                    jSAMObject = jSAMObjects.FirstOrDefault();
                    InvalidateClone();
                    result = jSAMObject != null;
                }
            }

            if(result)
            {
                OnOpened();
                modified = false;
            }

            return result;
        }

        public void OnOpening()
        {
            EventHandler eventHandler = Opening;
            if (eventHandler != null)
            {
                eventHandler(this, EventArgs.Empty);
            }
        }

        public void OnOpened()
        {
            OpenedEventHandler eventHandler;

            eventHandler = Opened;
            if (eventHandler != null)
            {
                eventHandler(this, new OpenedEventArgs());
            }
        }


        public bool Close()
        {
            OnClosing();

            if (modified && jSAMObject != null)
            {
                MessageBoxResult dialogResult = MessageBox.Show("Do you want to save before closing?", "Save", MessageBoxButton.YesNoCancel);
                if(dialogResult == MessageBoxResult.Cancel)
                {
                    return false;
                }

                if(dialogResult == MessageBoxResult.Yes)
                {
                    bool result = Save();
                    if(!result)
                    {
                        return false;
                    }
                }
            }

            jSAMObject = default;
            InvalidateClone();

            modified = false;
            OnClosed();

            return true;
        }

        public void OnClosing()
        {
            EventHandler eventHandler = Closing;
            if (eventHandler != null)
            {
                eventHandler(this, EventArgs.Empty);
            }
        }

        public void OnClosed()
        {
            ClosedEventHandler eventHandler;

            eventHandler = Closed;
            if (eventHandler != null)
            {
                eventHandler(this, new ClosedEventArgs());
            }
        }


        public bool Save()
        {
            OnSaving();

            if(jSAMObject == null)
            {
                return false;
            }

            if(string.IsNullOrWhiteSpace(path))
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog()
                {
                    Filter = "json files (*.json)|*.json|All files (*.*)|*.*",
                    FilterIndex = 1
                };

                if(saveFileDialog.ShowDialog() != true)
                {
                    return false;
                }

                path = saveFileDialog.FileName;
            }

            bool result = Core.Convert.ToFile(new IJSAMObject[] { jSAMObject }, path);
            if(!result)
            {
                return result;
            }

            modified = false;

            OnSaved();

            return result;
        }

        public void OnSaving()
        {
            EventHandler eventHandler = Saving;
            if (eventHandler != null)
            {
                eventHandler(this, EventArgs.Empty);
            }
        }

        public void OnSaved()
        {
            EventHandler eventHandler = Saved;
            if (eventHandler != null)
            {
                eventHandler(this, EventArgs.Empty);
            }
        }

        public void OnModified(IEnumerable<IModification> modifications = null)
        {
            IEnumerable<IModification> modifications_Temp = modifications;
            if(modifications_Temp == null || modifications_Temp.Count() == 0)
            {
                modifications_Temp = new List<IModification>() { new FullModification() };
            }
            
            ModifiedEventHandler modifiedEventHandler = Modified;
            if (modifiedEventHandler != null)
            {
                modifiedEventHandler(this, new ModifiedEventArgs(modifications_Temp));
            }
        }

    }
}
