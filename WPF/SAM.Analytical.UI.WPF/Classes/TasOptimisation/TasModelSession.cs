// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.GenOpt;
using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>How far the Design Optimisation window has read the Tas model ("tas-model" engine).</summary>
    public enum TasModelReadState
    {
        /// <summary>No Tas project folder has been read yet.</summary>
        NotRead,

        /// <summary>The model is being read (licensed Tas, in the background).</summary>
        Reading,

        /// <summary>The model was read: <see cref="TasModelSession.Inventory"/> and the catalogue are available.</summary>
        Ready,

        /// <summary>The model could not be read: <see cref="TasModelSession.Error"/> says why.</summary>
        Failed,
    }

    /// <summary>
    /// What the "tas-model" engine of the Design Optimisation window knows about the Tas model (PR8): the project's
    /// inventory (SAM_Tas <c>Query.TasModelInventory</c>, licensed Tas, read once per folder in the background), the glazing
    /// pool a glazing choice draws on (the SAM UI Glazing window's sources: the model, the default library, "My glazing
    /// systems" and loaded files, calculated with SAM_Tas <c>Query.TasGlazingSystems</c>), the editable glazing filter,
    /// and from these the model's catalogue (<c>Query.TasModelCatalogue</c>, pure). The runner it creates
    /// (<see cref="CreateRunner"/>) is given the same inventory, pool and filter, so a definition is run against exactly
    /// the catalogue it was checked against.
    /// <para>
    /// Not thread-affine: the background reads run on their own STA threads (Tas COM and TCD need one) and complete on
    /// the caller's synchronisation context (the window's dispatcher), where <see cref="Changed"/> is raised.
    /// </para>
    /// </summary>
    public sealed class TasModelSession
    {
        private readonly Func<string, TasModelInventory> readInventory;
        private readonly Func<GlazingSource, List<TasGlazingSystem>> calculateGlazing;
        private readonly List<GlazingSource> sources = new List<GlazingSource>();
        private readonly List<TasGlazingSystem> pool = new List<TasGlazingSystem>();
        private readonly List<string> notes = new List<string>();
        private TasGlazingFilter glazingFilter = new TasGlazingFilter();
        private OptimisationCatalogue? catalogue;
        private int version;
        private int poolReads;

        /// <param name="readInventory">Reads a project folder (tests); null for SAM_Tas' licensed reader.</param>
        /// <param name="calculateGlazing">Calculates a glazing source's systems (tests); null for SAM_Tas' TCD calculation.</param>
        public TasModelSession(Func<string, TasModelInventory>? readInventory = null, Func<GlazingSource, List<TasGlazingSystem>>? calculateGlazing = null)
        {
            this.readInventory = readInventory ?? Analytical.Tas.GenOpt.Query.TasModelInventory;
            this.calculateGlazing = calculateGlazing ?? (x => Analytical.Tas.GenOpt.Query.TasGlazingSystems(x.ConstructionManager, x.Label));
        }

        /// <summary>Raised (on the caller's context) when the state, the pool, the filter or the catalogue changes.</summary>
        public event EventHandler? Changed;

        public TasModelReadState State { get; private set; } = TasModelReadState.NotRead;

        /// <summary>The folder read (or being read); null before the first read.</summary>
        public string? Folder { get; private set; }

        /// <summary>Why the model could not be read; null unless <see cref="State"/> is <see cref="TasModelReadState.Failed"/>.</summary>
        public string? Error { get; private set; }

        /// <summary>The model's inventory; null unless <see cref="State"/> is <see cref="TasModelReadState.Ready"/>.</summary>
        public TasModelInventory? Inventory { get; private set; }

        /// <summary>The wall time of the last successful read.</summary>
        public TimeSpan ReadDuration { get; private set; }

        /// <summary>The glazing sources added to the pool, in the order they were added.</summary>
        public IReadOnlyList<GlazingSource> Sources => sources;

        /// <summary>The glazing pool: every calculated system of the sources, the first of a Guid winning.</summary>
        public IReadOnlyList<TasGlazingSystem> Pool => pool;

        /// <summary>True while glazing sources are being calculated.</summary>
        public bool PoolBusy => poolReads > 0;

        /// <summary>What the pool's sources said (an unreadable "My glazing systems", a calculation that failed).</summary>
        public IReadOnlyList<string> PoolNotes => notes;

        /// <summary>Which pool systems become options of a glazing choice. Setting it rebuilds the catalogue.</summary>
        public TasGlazingFilter GlazingFilter
        {
            get => glazingFilter;
            set
            {
                glazingFilter = value ?? new TasGlazingFilter();
                Rebuild();
            }
        }

        /// <summary>The model's catalogue for the "tas-model" engine; null until the model is read.</summary>
        public OptimisationCatalogue? Catalogue => catalogue;

        /// <summary>
        /// Reads <paramref name="folder"/> in the background (licensed Tas). A newer read supersedes an older one, whose
        /// result is dropped. Never throws: a failure is <see cref="TasModelReadState.Failed"/> with <see cref="Error"/>.
        /// </summary>
        public async Task ReadAsync(string folder)
        {
            int version_Read = Interlocked.Increment(ref version);

            Folder = folder;
            State = TasModelReadState.Reading;
            Error = null;
            Inventory = null;
            Rebuild();

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            TasModelInventory? inventory = null;
            string? error = null;
            try
            {
                inventory = await RunSta(() => readInventory(folder), "SAM Tas model reader");
            }
            catch (Exception exception)
            {
                error = ReadError(exception);
            }

            if (version_Read != Volatile.Read(ref version))
            {
                return;
            }

            ReadDuration = stopwatch.Elapsed;
            Inventory = inventory;
            Error = error;
            State = inventory == null ? TasModelReadState.Failed : TasModelReadState.Ready;
            Rebuild();
        }

        /// <summary>Forgets the model (another folder, or the engine switched to "tas-script"). The glazing pool is kept.</summary>
        public void Reset()
        {
            Interlocked.Increment(ref version);
            Folder = null;
            State = TasModelReadState.NotRead;
            Error = null;
            Inventory = null;
            Rebuild();
        }

        /// <summary>
        /// Adds glazing sources to the pool, calculating each in the background (licensed Tas TCD). A source whose
        /// calculation fails adds a note and no systems; the others still count.
        /// </summary>
        public async Task AddGlazingSourcesAsync(IEnumerable<GlazingSource> glazingSources)
        {
            List<GlazingSource> list = (glazingSources ?? Enumerable.Empty<GlazingSource>()).Where(x => x != null).ToList();
            if (list.Count == 0)
            {
                return;
            }

            poolReads++;
            Raise();
            try
            {
                foreach (GlazingSource glazingSource in list)
                {
                    sources.Add(glazingSource);
                    if (!string.IsNullOrWhiteSpace(glazingSource.Note))
                    {
                        notes.Add(glazingSource.Note);
                    }

                    if ((glazingSource.ConstructionManager?.ApertureConstructions?.Count ?? 0) == 0)
                    {
                        continue;
                    }

                    List<TasGlazingSystem> systems;
                    try
                    {
                        systems = await RunSta(() => calculateGlazing(glazingSource), "SAM glazing pool") ?? new List<TasGlazingSystem>();
                    }
                    catch (Exception exception)
                    {
                        notes.Add(string.Format(CultureInfo.InvariantCulture, "{0} could not be calculated: {1}", glazingSource.Label, Innermost(exception).Message));
                        continue;
                    }

                    HashSet<Guid> guids = new HashSet<Guid>(pool.Select(x => x.Guid));
                    pool.AddRange(systems.Where(x => x != null && guids.Add(x.Guid)));
                    Rebuild();
                }
            }
            finally
            {
                poolReads--;
                Rebuild();
            }
        }

        /// <summary>
        /// The glazing options of every glazing construction of the model, with the current filter (what the "Can
        /// change" list shows for a glazing choice); empty until the model is read.
        /// </summary>
        public List<KeyValuePair<TasGlazingConstructionInfo, List<TasGlazingOption>>> GlazingOptions()
        {
            List<KeyValuePair<TasGlazingConstructionInfo, List<TasGlazingOption>>> result = new List<KeyValuePair<TasGlazingConstructionInfo, List<TasGlazingOption>>>();
            foreach (TasGlazingConstructionInfo tasGlazingConstructionInfo in Inventory?.GlazingConstructions ?? new List<TasGlazingConstructionInfo>())
            {
                result.Add(new KeyValuePair<TasGlazingConstructionInfo, List<TasGlazingOption>>(tasGlazingConstructionInfo, Analytical.Tas.GenOpt.Query.TasGlazingOptions(tasGlazingConstructionInfo, pool, glazingFilter)));
            }

            return result;
        }

        /// <summary>
        /// The local run settings of a "tas-model" run on this model: the folder, the runs folder, TasGenExecute, and the
        /// inventory, pool and filter the catalogue was built with (so nothing is read again).
        /// </summary>
        public TasModelRunSettings RunSettings(string projectFolder, string? runsFolder, string? tasGenExecutePath)
        {
            return new TasModelRunSettings(projectFolder, string.IsNullOrWhiteSpace(runsFolder) ? null : runsFolder, string.IsNullOrWhiteSpace(tasGenExecutePath) ? null : tasGenExecutePath)
            {
                Inventory = Inventory,
                GlazingPool = pool.ToList(),
                GlazingFilter = glazingFilter,
            };
        }

        /// <summary>
        /// Writes the glazing systems into a run's snapshot TBD (tests replace it); null for SAM_Tas' licensed writer.
        /// </summary>
        public Action<string, IReadOnlyList<TasGlazingOption>>? GlazingWriter { get; set; }

        /// <summary>
        /// SAM_Tas' runner for <paramref name="optimisationDefinition"/> on this model. It checks the definition against the
        /// capabilities and this catalogue, resolves the glazing options and generates the script; nothing is read from
        /// Tas and no folder is created. It throws what SAM_Tas throws for a definition that cannot run.
        /// </summary>
        public TasModelRunner CreateRunner(OptimisationDefinition optimisationDefinition, string projectFolder, string? runsFolder, string? tasGenExecutePath)
        {
            if (Inventory == null)
            {
                throw new InvalidOperationException("The Tas model has not been read yet.");
            }

            TasModelRunner result = new TasModelRunner(optimisationDefinition, RunSettings(projectFolder, runsFolder, tasGenExecutePath));
            if (GlazingWriter != null)
            {
                result.GlazingWriter = GlazingWriter;
            }

            return result;
        }

        /// <summary>
        /// What to tell the user when the model cannot be read: Tas or its licence missing is the common cause, so a COM
        /// failure says so; any other failure (two TBD files, a missing folder) is SAM_Tas' own message.
        /// </summary>
        public static string ReadError(Exception exception)
        {
            Exception innermost = Innermost(exception);
            if (innermost is System.Runtime.InteropServices.COMException || innermost is System.Runtime.InteropServices.InvalidComObjectException || innermost is TypeLoadException || innermost is System.IO.FileNotFoundException fileNotFound && (fileNotFound.FileName ?? string.Empty).StartsWith("Interop.", StringComparison.OrdinalIgnoreCase))
            {
                return "Tas could not open the model: " + innermost.Message.Trim() + " Tas (with a licence) must be installed on this computer to read the model and to run the optimisation.";
            }

            return innermost.Message;
        }

        private void Rebuild()
        {
            catalogue = Inventory == null ? null : Analytical.Tas.GenOpt.Query.TasModelCatalogue(Inventory, pool, glazingFilter);
            Raise();
        }

        private void Raise()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private static Exception Innermost(Exception exception)
        {
            Exception result = exception;
            while ((result is AggregateException || result is System.Reflection.TargetInvocationException) && result.InnerException != null)
            {
                result = result.InnerException;
            }

            return result;
        }

        /// <summary>Runs <paramref name="func"/> on a new background STA thread (Tas COM and TCD need one).</summary>
        internal static Task<T> RunSta<T>(Func<T> func, string name)
        {
            TaskCompletionSource<T> completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Thread thread = new Thread(() =>
            {
                try
                {
                    completion.SetResult(func());
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            })
            {
                IsBackground = true,
                Name = name,
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            return completion.Task;
        }
    }
}
