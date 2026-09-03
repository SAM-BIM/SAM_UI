// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using System.Collections.Generic;
using System.Text;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// The Part O pre-flight: <c>SAM.Analytical.Create.Log</c> - the authority behind the
    /// <c>SAMAnalytical.Check</c> component and the Check command - run over the exact model that is about
    /// to be converted and simulated, and the one decision that follows from it.
    ///
    /// <para><b>Why this exists</b></para>
    /// <para>
    /// TAS runs its own pre-simulation check and refuses models that fail it. Several of the things it
    /// refuses for are properties of the SAM model, decided long before any TBD is written, and SAM can see
    /// them: a humidistat whose lower limit is above its upper limit is invalid on the analytical object
    /// itself. Discovering those in TAS means a person waits through a geometry conversion - 40 s on the
    /// licensed acceptance model, and far longer on a block of flats - to be told something that was
    /// knowable before it started, in TAS's words rather than in terms of the SAM object at fault.
    /// </para>
    /// <para>
    /// So the intended order is <b>prepared model -> Check -> TAS conversion -> TAS simulation</b>, and
    /// this is the Check step. It is deliberately the SAME authority the Check command shows, not a second
    /// list of Part O rules: a model state that is invalid is invalid whoever asks, and a rule that only
    /// existed on the Part O path would be a rule the Check command could not tell anyone about.
    /// </para>
    ///
    /// <para><b>Errors stop the run; warnings do not</b></para>
    /// <para>
    /// <see cref="LogRecordType.Error"/> is the only fatal level, exactly as <c>Create.Log</c> already
    /// assigns it. Everything else - Warning, Message, Undefined - is reported and the run proceeds.
    /// </para>
    /// <para>
    /// That distinction is load-bearing here. A Part O model deliberately leaves the generated MVHR plant
    /// zone inactive on the HDD and CDD design daytypes, and TAS says so ("Zone 'MVHR-01' is missing
    /// internal conditions on some daytypes"). It is a warning about an intentional state and must not
    /// become a reason not to simulate; nothing in this class promotes a warning.
    /// </para>
    ///
    /// <para><b>What passing does and does not mean</b></para>
    /// <para>
    /// Passing means the model carries no model-validity defect this Check knows how to detect. It is not a
    /// guarantee that TAS will run: licensing, file I/O, the solver, weather data and everything else
    /// external can still fail, and TAS's own check knows rules this one does not. Where TAS refuses a
    /// model for a deterministic model-validity problem SAM could have seen, the answer is to fix the
    /// source-model defect and add the missing rule to <c>Create.Log</c> - not to weaken this gate.
    /// </para>
    /// </summary>
    public class PartOPreSimulationCheck
    {
        private Log log;
        private List<LogRecord> logRecords_Error;
        private List<LogRecord> logRecords_Warning;

        /// <summary>
        /// Runs the check over <paramref name="analyticalModel"/>.
        /// <para>
        /// The model handed in is the one that must be checked, whatever it is. On an isolated run that is
        /// the DERIVED isolated model - the thing that will actually be converted - and not the
        /// full-building model it was extracted from; the caller passes the model it is about to simulate,
        /// and this class never goes looking for another one.
        /// </para>
        /// <para>
        /// Read-only: <c>Create.Log</c> inspects and reports. Nothing here modifies the model.
        /// </para>
        /// </summary>
        public PartOPreSimulationCheck(AnalyticalModel analyticalModel)
        {
            log = analyticalModel == null ? null : Analytical.Create.Log(analyticalModel);

            logRecords_Error = new List<LogRecord>();
            logRecords_Warning = new List<LogRecord>();

            if (log != null)
            {
                foreach (LogRecord logRecord in log)
                {
                    if (logRecord == null)
                    {
                        continue;
                    }

                    if (logRecord.LogRecordType == LogRecordType.Error)
                    {
                        logRecords_Error.Add(logRecord);
                    }
                    else if (logRecord.LogRecordType == LogRecordType.Warning)
                    {
                        logRecords_Warning.Add(logRecord);
                    }
                }
            }
        }

        /// <summary>
        /// <b>The gate decision</b>: the check to run before this simulation, or <b>null</b> where this
        /// simulation is not one the Part O pre-flight contract covers.
        ///
        /// <para><b>What it covers</b></para>
        /// <para>
        /// A <see cref="PartORunState.Prepared"/> run - which is precisely "the first TAS simulation of a
        /// prepared Part O run", and every re-prepared one after it. <c>Modify.Simulate</c> arms this with
        /// the session's run, and both optimisation call sites (an Iteration 2B round and the capacity
        /// envelope) call <c>PartORun.Prepare</c> immediately before simulating. So a full run, an isolated
        /// run, Iteration 1a, 1b and 2, every 2B round and the envelope are all covered.
        /// </para>
        ///
        /// <para><b>What it deliberately does not</b></para>
        /// <para>
        /// The ordinary Simulate command, which reaches the same method with no run or an unprepared one.
        /// Making SAM Check a hard gate on <i>every</i> TAS simulation in SAM is a larger change than the
        /// Part O contract: <c>Create.Log</c> reports Errors on states a long-standing model may well
        /// carry, and a model that simulates today must not stop simulating because the Part O path grew a
        /// gate. The Check command remains available to any model, on demand.
        /// </para>
        /// </summary>
        /// <param name="partORun">The run being simulated, or null where the caller has none.</param>
        /// <param name="analyticalModel">The model about to be converted - on an isolated run, the derived one.</param>
        public static PartOPreSimulationCheck Gate(PartORun partORun, AnalyticalModel analyticalModel)
        {
            if (partORun == null || partORun.State != PartORunState.Prepared)
            {
                return null;
            }

            return new PartOPreSimulationCheck(analyticalModel);
        }

        /// <summary>
        /// The full log, exactly as <c>Create.Log</c> produced it, for showing to the user. Null where no
        /// model was supplied.
        /// </summary>
        public Log Log
        {
            get
            {
                return log == null ? null : new Log(log);
            }
        }

        /// <summary>
        /// The fatal records - and only those. One of these is why a run does not start.
        /// </summary>
        public List<LogRecord> Errors
        {
            get
            {
                return logRecords_Error.ConvertAll(x => new LogRecord(x));
            }
        }

        /// <summary>
        /// The records that are worth saying and are not a reason to stop. Reported, never promoted.
        /// </summary>
        public List<LogRecord> Warnings
        {
            get
            {
                return logRecords_Warning.ConvertAll(x => new LogRecord(x));
            }
        }

        /// <summary>
        /// Whether the model may be simulated: true where the check found no <see cref="LogRecordType.Error"/>.
        /// <para>
        /// A model that could not be checked at all - none was supplied - is not valid, because "nothing
        /// was checked" must never read as "nothing was wrong".
        /// </para>
        /// </summary>
        public bool IsValid
        {
            get
            {
                return log != null && logRecords_Error.Count == 0;
            }
        }

        /// <summary>
        /// Why the run must not start, or null where it may.
        /// <para>
        /// States that it was the pre-simulation validation that failed, how many errors there were, and
        /// then the errors themselves - which carry the object's type, its name and its Guid, because that
        /// is how <c>Create.Log</c> writes them. Capped at <paramref name="maxCount"/> lines so a model
        /// with hundreds of defects still produces a message a person can read; the full list is in
        /// <see cref="Log"/>.
        /// </para>
        /// </summary>
        public string Refusal(int maxCount = 20)
        {
            if (IsValid)
            {
                return null;
            }

            if (log == null)
            {
                return "No model was supplied to the Part O pre-simulation check, so nothing could be validated and nothing was simulated.";
            }

            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendFormat(
                logRecords_Error.Count == 1
                    ? "The prepared model failed the Part O pre-simulation validation (SAMAnalytical.Check) with {0} error, so TAS was not started.\n\n"
                    : "The prepared model failed the Part O pre-simulation validation (SAMAnalytical.Check) with {0} errors, so TAS was not started.\n\n",
                logRecords_Error.Count);

            int count = System.Math.Min(maxCount < 1 ? 1 : maxCount, logRecords_Error.Count);
            for (int i = 0; i < count; i++)
            {
                stringBuilder.AppendFormat("- {0}\n", logRecords_Error[i].Text);
            }

            if (count < logRecords_Error.Count)
            {
                stringBuilder.AppendFormat("- ...and {0} more, listed in full in the validation report.\n", logRecords_Error.Count - count);
            }

            if (logRecords_Warning.Count != 0)
            {
                stringBuilder.AppendFormat(
                    logRecords_Warning.Count == 1
                        ? "\nThere is also 1 warning. Warnings do not prevent a simulation and did not stop this one - the errors above did.\n"
                        : "\nThere are also {0} warnings. Warnings do not prevent a simulation and did not stop this one - the errors above did.\n",
                    logRecords_Warning.Count);
            }

            stringBuilder.Append("\nFix the reported objects in the source model and prepare the iteration again.");

            return stringBuilder.ToString();
        }
    }
}
