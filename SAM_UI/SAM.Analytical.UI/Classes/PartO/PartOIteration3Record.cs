// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// The one durable statement of an Iteration 3 pairing: which Reference A run, which Candidate B
    /// artifacts, what scope was taken, what was bound to what, what each TM59 assessment said, and what
    /// happened at every stage.
    ///
    /// <para><b>One record per pairing, beside Reference A's results</b></para>
    /// <para>
    /// A is the run being compared against, and it is the thing a person reopens. The record therefore
    /// lives beside A's own TSD, named from it, exactly as the TM59 report and the run model already are -
    /// so a directory holds <c>&lt;run&gt;.tsd</c>, <c>&lt;run&gt;.sam</c>, <c>&lt;run&gt;-TM59.txt</c> and
    /// <c>&lt;run&gt;-Iteration3.json</c>, and no run can overwrite another's.
    /// </para>
    ///
    /// <para><b>It records, and decides nothing</b></para>
    /// <para>
    /// Every verdict in it is another authority's, copied: TM59's statuses, SAM_Systems' and SAM_Tas'
    /// refusals, the provider's own method name. Reopening validates the record and then <b>re-runs the
    /// existing assessments over the existing results</b> rather than believing the numbers written here -
    /// see <c>Modify.ReviewPartOIteration3</c>. That is why no thermal statistic is stored: a stored
    /// statistic is a second answer waiting to disagree with the one a review computes.
    /// </para>
    ///
    /// <para><b>Artifacts are this attempt's</b></para>
    /// <para>
    /// <see cref="Files"/> carries only paths the attempt that wrote this record demonstrably created or
    /// updated, with the length and write time that prove it - see <see cref="PartOIteration3Artifacts"/>.
    /// A file left at a fixed path by an earlier attempt never reaches this record.
    /// </para>
    /// </summary>
    public class PartOIteration3Record
    {
        /// <summary>
        /// The schema this writer produces. A record written by a different schema is refused on review
        /// rather than read optimistically - a field that moved would otherwise be read as a default and
        /// validate.
        /// </summary>
        public const string CurrentSchema = "PartOIteration3Record:v1";

        private readonly List<Guid> guids_VentilationSystem_Prepared = [];

        private readonly List<Guid> guids_VentilationSystem_ScopedOut = [];

        private readonly List<string> notes_Scope = [];

        private readonly List<PartOIteration3BindingRecord> bindings = [];

        private readonly List<PartOIteration3FileRecord> files = [];

        private readonly List<PartOIteration3StageState> stages = [];

        public PartOIteration3Record()
        {
        }

        /// <summary>The schema of this record as it was read or written.</summary>
        public string Schema { get; set; } = CurrentSchema;

        /// <summary>The attempt that produced it.</summary>
        public Guid Guid_Run { get; set; }

        /// <summary>When, in UTC ticks.</summary>
        public long Ticks_Utc { get; set; }

        //---------------------------------------------------------------------------------------------
        //Reference A provenance
        //---------------------------------------------------------------------------------------------

        /// <summary>Reference A's results file.</summary>
        public string Path_TSD_ReferenceA { get; set; }

        /// <summary>Reference A's persisted model, where it wrote one.</summary>
        public string Path_Model_ReferenceA { get; set; }

        /// <summary>
        /// The design-state fingerprint Reference A's own <c>SimulationResultProvenance</c> recorded. The
        /// pairing is valid only against that exact design state.
        /// </summary>
        public string Fingerprint_Model_ReferenceA { get; set; }

        /// <summary>The overheating-scenario fingerprint the same record carried.</summary>
        public string Fingerprint_Scenarios_ReferenceA { get; set; }

        //---------------------------------------------------------------------------------------------
        //The thermal case both sides were run as
        //---------------------------------------------------------------------------------------------

        /// <summary>
        /// The TAS case, spelled out: weather, solar method, day range and the workflow options that
        /// change a result. Candidate B is only comparable because it inherited every one of them, and a
        /// review re-checks that the record it is reading states the case the run it is attached to used.
        /// </summary>
        public string Fingerprint_Scenario { get; set; }

        /// <summary>Reference A's project name.</summary>
        public string ProjectName_ReferenceA { get; set; }

        /// <summary>Candidate B's project name - what makes its files its own.</summary>
        public string ProjectName_CandidateB { get; set; }

        //---------------------------------------------------------------------------------------------
        //Scope - SAM #114
        //---------------------------------------------------------------------------------------------

        /// <summary>
        /// The ventilation systems the Part O preparation built, captured when the run adopted it. These
        /// and only these are the design under assessment - never a system chosen by display name.
        /// </summary>
        public List<Guid> Guids_VentilationSystem_Prepared => [.. guids_VentilationSystem_Prepared];

        /// <summary>The authored systems removed from the PR1 working copy, by identity.</summary>
        public List<Guid> Guids_VentilationSystem_ScopedOut => [.. guids_VentilationSystem_ScopedOut];

        /// <summary>Why each scoped-out system was not mechanical duty Candidate B had to recreate.</summary>
        public List<string> Notes_Scope => [.. notes_Scope];

        //---------------------------------------------------------------------------------------------
        //Candidate B
        //---------------------------------------------------------------------------------------------

        /// <summary>One row per room of the explicit route.</summary>
        public List<PartOIteration3BindingRecord> Bindings => [.. bindings];

        /// <summary>How many supply legs the conversion reconciled.</summary>
        public int Count_Connection_Supply { get; set; }

        /// <summary>How many extract legs.</summary>
        public int Count_Connection_Extract { get; set; }

        /// <summary>How many transfer legs.</summary>
        public int Count_Connection_Transfer { get; set; }

        /// <summary>How many air systems - one per physical analytical air handling unit.</summary>
        public int Count_AirSystem { get; set; }

        /// <summary>Whether the no-IZAM source swept inherited IZAMs.</summary>
        public bool RemovedIZAMs { get; set; }

        /// <summary>Whether it neutralised the mechanical ventilation gain.</summary>
        public bool RemovedMechanicalVentilationGains { get; set; }

        /// <summary>How the resultant temperatures were obtained, in the provider's own words.</summary>
        public string Method_ResultantTemperature { get; set; }

        /// <summary>
        /// Whether the provider's own series and the values TM59 subsequently read out of Candidate B's
        /// result file were identical for every bound room and every hour. A pairing whose two readings of
        /// the same numbers disagree is refused before reconciliation, so this is true on every complete
        /// record - it is written so a reader can see it was asked.
        /// </summary>
        public bool ProviderMatchesResultFile { get; set; }

        /// <summary>How many (room, hour) values that identity check compared.</summary>
        public long Count_ProviderIdentityValues { get; set; }

        //---------------------------------------------------------------------------------------------
        //Verdicts and files
        //---------------------------------------------------------------------------------------------

        /// <summary>Reference A's combined occupied-space verdict, carried from its own report.</summary>
        public TM59ComplianceStatus Status_ReferenceA { get; set; } = TM59ComplianceStatus.Undefined;

        /// <summary>Candidate B's combined occupied-space verdict, carried from its own report.</summary>
        public TM59ComplianceStatus Status_CandidateB { get; set; } = TM59ComplianceStatus.Undefined;

        /// <summary>Every file this attempt created or updated, with the fingerprint a review validates.</summary>
        public List<PartOIteration3FileRecord> Files => [.. files];

        /// <summary>The ordered stage ledger, exactly as it stood when the run finished.</summary>
        public List<PartOIteration3StageState> Stages => [.. stages];

        /// <summary>The refused stage, or null.</summary>
        public PartOIteration3Stage? Stage_Refused { get; set; }

        /// <summary>Whether the pairing completed every stage. Only then is a review allowed to rebuild it.</summary>
        public bool IsComplete { get; set; }

        //---------------------------------------------------------------------------------------------
        //Assembly
        //---------------------------------------------------------------------------------------------

        public void Add(PartOIteration3FileRecord partOIteration3FileRecord)
        {
            if (partOIteration3FileRecord is not null)
            {
                files.Add(partOIteration3FileRecord);
            }
        }

        public void Add(PartOIteration3BindingRecord partOIteration3BindingRecord)
        {
            if (partOIteration3BindingRecord is not null)
            {
                bindings.Add(partOIteration3BindingRecord);
            }
        }

        public void AddPreparedSystems(IEnumerable<Guid> guids)
        {
            foreach (Guid guid in guids ?? [])
            {
                if (guid != Guid.Empty && !guids_VentilationSystem_Prepared.Contains(guid))
                {
                    guids_VentilationSystem_Prepared.Add(guid);
                }
            }
        }

        public void AddScopedOutSystems(IEnumerable<Guid> guids)
        {
            foreach (Guid guid in guids ?? [])
            {
                if (guid != Guid.Empty && !guids_VentilationSystem_ScopedOut.Contains(guid))
                {
                    guids_VentilationSystem_ScopedOut.Add(guid);
                }
            }
        }

        public void AddScopeNotes(IEnumerable<string> notes)
        {
            foreach (string note in notes ?? [])
            {
                if (!string.IsNullOrWhiteSpace(note))
                {
                    notes_Scope.Add(note);
                }
            }
        }

        /// <summary>Takes the ledger as it stands. Replaces anything previously taken.</summary>
        public void Adopt(PartOIteration3Ledger partOIteration3Ledger)
        {
            stages.Clear();

            if (partOIteration3Ledger is null)
            {
                Stage_Refused = null;
                IsComplete = false;

                return;
            }

            stages.AddRange(partOIteration3Ledger.Stages);

            Stage_Refused = partOIteration3Ledger.Stage_Refused;
            IsComplete = partOIteration3Ledger.IsComplete;
        }

        /// <summary>One file record by role, or null.</summary>
        public PartOIteration3FileRecord File(string role)
        {
            foreach (PartOIteration3FileRecord partOIteration3FileRecord in files)
            {
                if (string.Equals(partOIteration3FileRecord.Role, role, StringComparison.Ordinal))
                {
                    return partOIteration3FileRecord;
                }
            }

            return null;
        }

        //---------------------------------------------------------------------------------------------
        //Persistence
        //---------------------------------------------------------------------------------------------

        public JsonObject ToJsonObject()
        {
            JsonArray jsonArray_Bindings = [];
            foreach (PartOIteration3BindingRecord partOIteration3BindingRecord in bindings)
            {
                jsonArray_Bindings.Add(partOIteration3BindingRecord.ToJsonObject());
            }

            JsonArray jsonArray_Files = [];
            foreach (PartOIteration3FileRecord partOIteration3FileRecord in files)
            {
                jsonArray_Files.Add(partOIteration3FileRecord.ToJsonObject());
            }

            JsonArray jsonArray_Stages = [];
            foreach (PartOIteration3StageState partOIteration3StageState in stages)
            {
                jsonArray_Stages.Add(new JsonObject
                {
                    { "Stage", partOIteration3StageState.Stage.ToString() },
                    { "Status", partOIteration3StageState.Status.ToString() },
                    { "Detail", partOIteration3StageState.Detail },
                    { "Reasons", PartOIteration3Json.Array(partOIteration3StageState.Reasons) },
                    { "Artifacts", PartOIteration3Json.Array(partOIteration3StageState.Artifacts) },
                });
            }

            return new JsonObject
            {
                { "Schema", Schema },
                { "Guid_Run", Guid_Run.ToString() },
                { "Ticks_Utc", Ticks_Utc },
                { "Path_TSD_ReferenceA", Path_TSD_ReferenceA },
                { "Path_Model_ReferenceA", Path_Model_ReferenceA },
                { "Fingerprint_Model_ReferenceA", Fingerprint_Model_ReferenceA },
                { "Fingerprint_Scenarios_ReferenceA", Fingerprint_Scenarios_ReferenceA },
                { "Fingerprint_Scenario", Fingerprint_Scenario },
                { "ProjectName_ReferenceA", ProjectName_ReferenceA },
                { "ProjectName_CandidateB", ProjectName_CandidateB },
                { "Guids_VentilationSystem_Prepared", PartOIteration3Json.Array(guids_VentilationSystem_Prepared) },
                { "Guids_VentilationSystem_ScopedOut", PartOIteration3Json.Array(guids_VentilationSystem_ScopedOut) },
                { "Notes_Scope", PartOIteration3Json.Array(notes_Scope) },
                { "Bindings", jsonArray_Bindings },
                { "Count_Connection_Supply", Count_Connection_Supply },
                { "Count_Connection_Extract", Count_Connection_Extract },
                { "Count_Connection_Transfer", Count_Connection_Transfer },
                { "Count_AirSystem", Count_AirSystem },
                { "RemovedIZAMs", RemovedIZAMs },
                { "RemovedMechanicalVentilationGains", RemovedMechanicalVentilationGains },
                { "Method_ResultantTemperature", Method_ResultantTemperature },
                { "ProviderMatchesResultFile", ProviderMatchesResultFile },
                { "Count_ProviderIdentityValues", Count_ProviderIdentityValues },
                { "Status_ReferenceA", Status_ReferenceA.ToString() },
                { "Status_CandidateB", Status_CandidateB.ToString() },
                { "Files", jsonArray_Files },
                { "Stages", jsonArray_Stages },
                { "Stage_Refused", Stage_Refused.HasValue ? Stage_Refused.Value.ToString() : null },
                { "IsComplete", IsComplete },
            };
        }

        public static PartOIteration3Record FromJsonObject(JsonObject jsonObject)
        {
            if (jsonObject is null)
            {
                return null;
            }

            PartOIteration3Record result = new()
            {
                Schema = PartOIteration3Json.Text(jsonObject, "Schema"),
                Guid_Run = PartOIteration3Json.Guid(jsonObject, "Guid_Run"),
                Ticks_Utc = PartOIteration3Json.Integer(jsonObject, "Ticks_Utc", -1),
                Path_TSD_ReferenceA = PartOIteration3Json.Text(jsonObject, "Path_TSD_ReferenceA"),
                Path_Model_ReferenceA = PartOIteration3Json.Text(jsonObject, "Path_Model_ReferenceA"),
                Fingerprint_Model_ReferenceA = PartOIteration3Json.Text(jsonObject, "Fingerprint_Model_ReferenceA"),
                Fingerprint_Scenarios_ReferenceA = PartOIteration3Json.Text(jsonObject, "Fingerprint_Scenarios_ReferenceA"),
                Fingerprint_Scenario = PartOIteration3Json.Text(jsonObject, "Fingerprint_Scenario"),
                ProjectName_ReferenceA = PartOIteration3Json.Text(jsonObject, "ProjectName_ReferenceA"),
                ProjectName_CandidateB = PartOIteration3Json.Text(jsonObject, "ProjectName_CandidateB"),
                Count_Connection_Supply = PartOIteration3Json.Count(jsonObject, "Count_Connection_Supply", 0),
                Count_Connection_Extract = PartOIteration3Json.Count(jsonObject, "Count_Connection_Extract", 0),
                Count_Connection_Transfer = PartOIteration3Json.Count(jsonObject, "Count_Connection_Transfer", 0),
                Count_AirSystem = PartOIteration3Json.Count(jsonObject, "Count_AirSystem", 0),
                RemovedIZAMs = PartOIteration3Json.Boolean(jsonObject, "RemovedIZAMs", false),
                RemovedMechanicalVentilationGains = PartOIteration3Json.Boolean(jsonObject, "RemovedMechanicalVentilationGains", false),
                Method_ResultantTemperature = PartOIteration3Json.Text(jsonObject, "Method_ResultantTemperature"),
                ProviderMatchesResultFile = PartOIteration3Json.Boolean(jsonObject, "ProviderMatchesResultFile", false),
                Count_ProviderIdentityValues = PartOIteration3Json.Integer(jsonObject, "Count_ProviderIdentityValues", 0),
                Status_ReferenceA = PartOIteration3Json.Enum(jsonObject, "Status_ReferenceA", TM59ComplianceStatus.Undefined),
                Status_CandidateB = PartOIteration3Json.Enum(jsonObject, "Status_CandidateB", TM59ComplianceStatus.Undefined),
                IsComplete = PartOIteration3Json.Boolean(jsonObject, "IsComplete", false),
            };

            result.AddPreparedSystems(PartOIteration3Json.Guids(jsonObject, "Guids_VentilationSystem_Prepared"));
            result.AddScopedOutSystems(PartOIteration3Json.Guids(jsonObject, "Guids_VentilationSystem_ScopedOut"));
            result.AddScopeNotes(PartOIteration3Json.Texts(jsonObject, "Notes_Scope"));

            foreach (JsonObject jsonObject_Binding in PartOIteration3Json.Objects(jsonObject, "Bindings"))
            {
                result.Add(PartOIteration3BindingRecord.FromJsonObject(jsonObject_Binding));
            }

            foreach (JsonObject jsonObject_File in PartOIteration3Json.Objects(jsonObject, "Files"))
            {
                result.Add(PartOIteration3FileRecord.FromJsonObject(jsonObject_File));
            }

            foreach (JsonObject jsonObject_Stage in PartOIteration3Json.Objects(jsonObject, "Stages"))
            {
                result.stages.Add(new PartOIteration3StageState(
                    PartOIteration3Json.Enum(jsonObject_Stage, "Stage", PartOIteration3Stage.Input),
                    PartOIteration3Json.Enum(jsonObject_Stage, "Status", PartOIteration3StageStatus.NotRun),
                    PartOIteration3Json.Text(jsonObject_Stage, "Detail"),
                    PartOIteration3Json.Texts(jsonObject_Stage, "Reasons"),
                    PartOIteration3Json.Texts(jsonObject_Stage, "Artifacts")));
            }

            string text_Stage_Refused = PartOIteration3Json.Text(jsonObject, "Stage_Refused");
            result.Stage_Refused = Enum.TryParse(text_Stage_Refused, false, out PartOIteration3Stage partOIteration3Stage) ? partOIteration3Stage : (PartOIteration3Stage?)null;

            return result;
        }

        /// <summary>
        /// The record as text. Indented, because a person auditing a pairing reads this file, and a
        /// one-line JSON blob is not readable evidence.
        /// </summary>
        public override string ToString()
        {
            return ToJsonObject().ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>Reads one back. Null where the text is not a JSON object at all.</summary>
        public static PartOIteration3Record Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            try
            {
                return FromJsonObject(JsonNode.Parse(text) as JsonObject);
            }
            catch (JsonException)
            {
                //A record that is not JSON is refused on review by name, which is more useful than an
                //exception escaping from the middle of reopening a model.
                return null;
            }
        }
    }
}
