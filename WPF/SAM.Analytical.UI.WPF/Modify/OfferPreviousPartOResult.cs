// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>What the person chose when a previous result of the same design was found.</summary>
    internal enum PartOPreviousResultChoice
    {
        /// <summary>Open the saved result model; nothing is simulated.</summary>
        OpenPrevious,

        /// <summary>Carry on into the ordinary run, whose replace-existing-results question follows.</summary>
        RunAgain,

        /// <summary>Do nothing.</summary>
        Cancel,
    }

    /// <summary>What <see cref="Modify.OfferPreviousPartOResult"/> decided.</summary>
    internal enum PartOPreviousResultDecision
    {
        /// <summary>No previous result of this design was found: go on, ask nothing.</summary>
        None,

        /// <summary>Open <c>Path_Open</c>.</summary>
        Open,

        /// <summary>Run again: the existing replacement flow takes over.</summary>
        RunAgain,

        /// <summary>The person cancelled: nothing is changed and nothing is run.</summary>
        Cancel,
    }

    /// <summary>A saved Part O result model that SAM confirmed was produced from the current design.</summary>
    internal sealed record PartOPreviousResult(string Path_Model, string Name, DateTime LastWriteUtc);

    public static partial class Modify
    {
        /// <summary>The most result models in a case's <c>tas</c> folder that are opened to look for the design's key.</summary>
        private const int Count_PreviousResult_Max = 4;

        /// <summary>
        /// Before a Part O case is run: does its output folder already hold a saved result of <b>this</b> design? Where it does, the
        /// person is asked once - open it, run again, or cancel - instead of being sent straight to "replace existing results".
        /// Pure decision: it opens nothing in the application, deletes nothing and writes nothing; <paramref name="ask"/> is the
        /// question, so a test answers it without a window.
        ///
        /// <para><b>Compatibility is SAM's, not the UI's</b></para>
        /// <para>
        /// A saved result is offered only when the design key it recorded (<c>PartOBaselineReference.Design.DesignKey</c>, taken by
        /// <c>Query.PartODesignKey</c> when the result was made) is the key <c>Query.PartODesignKey</c> gives the design now, <b>and</b> its
        /// own record still validates as a reviewable result (<c>SimulationResultProvenance.TryResolvePath_TSD</c>) - so the offer is never
        /// one the existing reopen would then refuse. A result with no recorded key (saved before it existed) is never claimed
        /// compatible: it can still be opened by hand, as before. A result derived from another result (Iteration 2B, Iteration 3) is
        /// not a result of the design and is not offered.
        /// </para>
        /// </summary>
        /// <param name="partOOutputPaths">Where the run would write, or null where it has no case folder (nothing to find).</param>
        /// <param name="design">The open design model. Not modified.</param>
        /// <param name="ask">Shows the found result and returns the person's choice.</param>
        /// <param name="path_Open">The result model to open where the decision is <see cref="PartOPreviousResultDecision.Open"/>; otherwise null.</param>
        internal static PartOPreviousResultDecision OfferPreviousPartOResult(PartOOutputPaths? partOOutputPaths, AnalyticalModel? design, Func<PartOPreviousResult, PartOPreviousResultChoice> ask, out string? path_Open)
        {
            path_Open = null;

            PartOPreviousResult? partOPreviousResult = FindPreviousPartOResult(partOOutputPaths, design);
            if (partOPreviousResult is null)
            {
                return PartOPreviousResultDecision.None;
            }

            switch (ask(partOPreviousResult))
            {
                case PartOPreviousResultChoice.OpenPrevious:
                    path_Open = partOPreviousResult.Path_Model;
                    return PartOPreviousResultDecision.Open;

                case PartOPreviousResultChoice.RunAgain:
                    return PartOPreviousResultDecision.RunAgain;

                default:
                    return PartOPreviousResultDecision.Cancel;
            }
        }

        /// <summary>
        /// The whole "previous result found" step of Prepare &amp; Run: offer, then act on the choice. Returns true where the run goes
        /// on (nothing was found, or <i>Run again</i> - the replacement question follows as ever) and false where it stops (the person
        /// cancelled, or opened the saved result).
        /// </summary>
        /// <param name="partOOutputPaths">Where the run would write, or null.</param>
        /// <param name="design">The open design model.</param>
        /// <param name="iteration">The case, as the Hub names it.</param>
        /// <param name="open">Opens a saved model in the application window (the File &gt; Open path). Null where the host cannot: nothing is offered then.</param>
        /// <param name="ask">Shows the found result and returns the person's choice.</param>
        /// <param name="opened">Whether the saved result replaced the open model - the caller must then leave the workflow.</param>
        /// <param name="outcome">The Hub's line where the step stopped the run and has something to say; otherwise null.</param>
        internal static bool HandlePreviousPartOResult(PartOOutputPaths? partOOutputPaths, AnalyticalModel? design, string? iteration, Func<string, bool>? open, Func<PartOPreviousResult, PartOPreviousResultChoice> ask, out bool opened, out PartOWorkflowOutcome? outcome)
        {
            opened = false;
            outcome = null;

            if (open is null)
            {
                return true;
            }

            switch (OfferPreviousPartOResult(partOOutputPaths, design, ask, out string? path_Open))
            {
                case PartOPreviousResultDecision.Open:
                    opened = open(path_Open!);
                    if (!opened)
                    {
                        outcome = PreviousResultNotOpenedOutcome(iteration);
                    }

                    return false;

                case PartOPreviousResultDecision.Cancel:
                    return false;

                default:
                    return true;
            }
        }

        /// <summary>
        /// The newest saved result model in the case's <c>tas</c> folder that was produced from <paramref name="design"/> - see
        /// <see cref="OfferPreviousPartOResult"/> - or null. Never throws: whatever cannot be read is simply not offered.
        /// </summary>
        internal static PartOPreviousResult? FindPreviousPartOResult(PartOOutputPaths? partOOutputPaths, AnalyticalModel? design)
        {
            if (partOOutputPaths is null || design is null || !Directory.Exists(partOOutputPaths.Directory_Tas))
            {
                return null;
            }

            try
            {
                //Cheap first: a result model is the .sam beside a results (.tsd) file. The baseline's own name only -
                //an optimisation round (-Opt01, -OptMax) is a result of a result, and the prepared model has no .tsd.
                List<(string Path, DateTime LastWriteUtc)> candidates = [];
                foreach (string path_TSD in Directory.EnumerateFiles(partOOutputPaths.Directory_Tas, "*.tsd", SearchOption.TopDirectoryOnly))
                {
                    string? path_Model = Query.Path_PartORunModel(path_TSD);
                    if (path_Model is null || !File.Exists(path_Model) || IsOptimisationRound(path_Model))
                    {
                        continue;
                    }

                    candidates.Add((path_Model, File.GetLastWriteTimeUtc(path_Model)));
                }

                if (candidates.Count == 0)
                {
                    return null;
                }

                //Costly, so only now that something could match: the key walks the whole design.
                string? designKey = Analytical.Query.PartODesignKey(design);
                if (string.IsNullOrEmpty(designKey))
                {
                    return null;
                }

                foreach ((string path, DateTime lastWriteUtc) in candidates.OrderByDescending(x => x.LastWriteUtc).Take(Count_PreviousResult_Max))
                {
                    AnalyticalModel? result = null;
                    try
                    {
                        result = Core.Convert.ToSAM<AnalyticalModel>(path)?.FirstOrDefault(x => x is not null);
                    }
                    catch
                    {
                        result = null;
                    }

                    if (IsResultOfDesign(result, path, designKey))
                    {
                        return new PartOPreviousResult(path, Path.GetFileNameWithoutExtension(path), lastWriteUtc);
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// Whether <paramref name="result"/> was derived straight from the design whose key is <paramref name="designKey"/> and would reopen as
        /// a reviewable result. A result with no recorded key is not a match: nothing is inferred.
        /// </summary>
        private static bool IsResultOfDesign(AnalyticalModel? result, string path, string designKey)
        {
            if (result is null)
            {
                return false;
            }

            if (!result.TryGetValue(Analytical.AnalyticalModelParameter.SimulationResultProvenance, out SimulationResultProvenance simulationResultProvenance) || simulationResultProvenance is null)
            {
                return false;
            }

            if (!result.TryGetValue(Analytical.AnalyticalModelParameter.PartOBaselineReference, out PartOBaselineReference partOBaselineReference) || partOBaselineReference is null)
            {
                return false;
            }

            //Derived from the design directly (1a, 1b, 2, Mixed Design), never from another result.
            PartOModelReference? design = partOBaselineReference.Design;
            if (partOBaselineReference.Source is not null || design is null || design.Kind != PartOModelReferenceKind.Design || string.IsNullOrEmpty(design.DesignKey))
            {
                return false;
            }

            if (!string.Equals(design.DesignKey, designKey, StringComparison.Ordinal))
            {
                return false;
            }

            //The record must still validate - the results file, its length and write time - or the existing reopen would refuse it.
            return simulationResultProvenance.TryResolvePath_TSD(result, path, out string _, out string _);
        }

        private static bool IsOptimisationRound(string path_Model)
        {
            string name = Path.GetFileNameWithoutExtension(path_Model);

            int index = name.LastIndexOf("-Opt", StringComparison.OrdinalIgnoreCase);

            return index >= 0 && (name.EndsWith("-OptMax", StringComparison.OrdinalIgnoreCase) || name.Skip(index + 4).All(char.IsDigit));
        }

        /// <summary>The question, as a window.</summary>
        internal static PartOPreviousResultChoice ConfirmOpenPreviousPartOResult(string caseName, PartOPreviousResult partOPreviousResult, IWin32Window? owner)
        {
            PartOPreviousResultWindow partOPreviousResultWindow = new(caseName, partOPreviousResult);

            if (owner is not null)
            {
                new System.Windows.Interop.WindowInteropHelper(partOPreviousResultWindow).Owner = owner.Handle;
            }

            partOPreviousResultWindow.ShowDialog();

            return partOPreviousResultWindow.Choice;
        }

        /// <summary>The Hub's line when the saved result was asked to be opened and could not be.</summary>
        internal static PartOWorkflowOutcome PreviousResultNotOpenedOutcome(string? iteration)
        {
            return new PartOWorkflowOutcome(
                PartOWorkflowOutcomeKind.Information,
                "○",
                string.Format("{0} previous result could not be opened", Name(iteration)),
                "Open the saved result model by hand (File > Open), or press Prepare & Run again and run it again · nothing was changed");
        }
    }
}
