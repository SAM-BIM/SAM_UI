// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.UI;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// <b>The pairing record survives being written and read back.</b>
    /// <para>
    /// It is the only durable statement of an Iteration 3 pairing, and a review in a later session acts
    /// on it: which files to validate, which rooms were compared, which dwelling each belongs to, what
    /// refused and why. A field that round-tripped as a default would make the review validate less than
    /// it appears to - and pass.
    /// </para>
    /// </summary>
    public class PartOIteration3RecordTests
    {
        private static readonly Guid guid_Space = new("aaaaaaaa-0000-0000-0000-000000000001");

        private static readonly Guid guid_Dwelling = new("11111111-1111-1111-1111-111111111111");

        private static PartOIteration3Record Record()
        {
            PartOIteration3Ledger partOIteration3Ledger = new();

            partOIteration3Ledger.Complete(PartOIteration3Stage.Input, "ready", ["C:\\out\\Flat-It3B.tbd (created)"]);
            partOIteration3Ledger.Complete(PartOIteration3Stage.ReferenceA, "A");
            partOIteration3Ledger.Refuse(PartOIteration3Stage.ReferenceATM59, "no", ["Reference A could not be assessed."]);

            PartOIteration3Record result = new()
            {
                Guid_Run = new Guid("99999999-8888-7777-6666-555555555555"),
                Ticks_Utc = 638_000_000_000_000_000L,
                Path_TSD_ReferenceA = "C:\\out\\Flat.tsd",
                Path_Model_ReferenceA = "C:\\out\\Flat.sam",
                Fingerprint_Model_ReferenceA = "0123456789abcdef",
                Fingerprint_Scenarios_ReferenceA = "fedcba9876543210",
                Fingerprint_Scenario = "weather=CIBSE 2021 Leeds_TRY | solar=TAS | days 1-365",
                ProjectName_ReferenceA = "Flat",
                ProjectName_CandidateB = "Flat-It3B",
                Count_Connection_Supply = 6,
                Count_Connection_Extract = 4,
                Count_Connection_Transfer = 4,
                Count_AirSystem = 3,
                RemovedIZAMs = true,
                RemovedMechanicalVentilationGains = true,
                Method_ResultantTemperature = "the Approved Document O thermostat bridge",
                ProviderMatchesResultFile = true,
                Count_ProviderIdentityValues = 70_080L,
                Status_ReferenceA = TM59ComplianceStatus.Pass,
                Status_CandidateB = TM59ComplianceStatus.Fail,
            };

            result.AddPreparedSystems([guid_Dwelling]);
            result.AddScopedOutSystems([guid_Space]);
            result.AddScopeNotes(["Ventilation system 'NV 1' was left out of the materialisation input."]);

            result.Add(new PartOIteration3BindingRecord(guid_Space, "Bedroom 2", guid_Dwelling, "Flat 1", Guid.NewGuid(), Guid.NewGuid(), "zone-1", 13.0, null));
            result.Add(new PartOIteration3FileRecord(PartOIteration3Roles.Bridge_TSD, "C:\\out\\Flat-It3B-Bridge.tsd", 12345L, 638_100_000_000_000_000L));

            result.Adopt(partOIteration3Ledger);

            return result;
        }

        [Fact]
        public void Every_field_survives_a_round_trip_through_its_own_text()
        {
            PartOIteration3Record partOIteration3Record = Record();

            PartOIteration3Record result = PartOIteration3Record.Parse(partOIteration3Record.ToString());

            Assert.NotNull(result);

            Assert.Equal(PartOIteration3Record.CurrentSchema, result.Schema);
            Assert.Equal(partOIteration3Record.Guid_Run, result.Guid_Run);
            Assert.Equal(partOIteration3Record.Ticks_Utc, result.Ticks_Utc);
            Assert.Equal(partOIteration3Record.Path_TSD_ReferenceA, result.Path_TSD_ReferenceA);
            Assert.Equal(partOIteration3Record.Path_Model_ReferenceA, result.Path_Model_ReferenceA);
            Assert.Equal(partOIteration3Record.Fingerprint_Model_ReferenceA, result.Fingerprint_Model_ReferenceA);
            Assert.Equal(partOIteration3Record.Fingerprint_Scenarios_ReferenceA, result.Fingerprint_Scenarios_ReferenceA);
            Assert.Equal(partOIteration3Record.Fingerprint_Scenario, result.Fingerprint_Scenario);
            Assert.Equal(partOIteration3Record.ProjectName_ReferenceA, result.ProjectName_ReferenceA);
            Assert.Equal(partOIteration3Record.ProjectName_CandidateB, result.ProjectName_CandidateB);
            Assert.Equal(partOIteration3Record.Count_Connection_Supply, result.Count_Connection_Supply);
            Assert.Equal(partOIteration3Record.Count_Connection_Extract, result.Count_Connection_Extract);
            Assert.Equal(partOIteration3Record.Count_Connection_Transfer, result.Count_Connection_Transfer);
            Assert.Equal(partOIteration3Record.Count_AirSystem, result.Count_AirSystem);
            Assert.Equal(partOIteration3Record.RemovedIZAMs, result.RemovedIZAMs);
            Assert.Equal(partOIteration3Record.RemovedMechanicalVentilationGains, result.RemovedMechanicalVentilationGains);
            Assert.Equal(partOIteration3Record.Method_ResultantTemperature, result.Method_ResultantTemperature);
            Assert.Equal(partOIteration3Record.ProviderMatchesResultFile, result.ProviderMatchesResultFile);
            Assert.Equal(partOIteration3Record.Count_ProviderIdentityValues, result.Count_ProviderIdentityValues);
            Assert.Equal(partOIteration3Record.Status_ReferenceA, result.Status_ReferenceA);
            Assert.Equal(partOIteration3Record.Status_CandidateB, result.Status_CandidateB);
            Assert.Equal(partOIteration3Record.Guids_VentilationSystem_Prepared, result.Guids_VentilationSystem_Prepared);
            Assert.Equal(partOIteration3Record.Guids_VentilationSystem_ScopedOut, result.Guids_VentilationSystem_ScopedOut);
            Assert.Equal(partOIteration3Record.Notes_Scope, result.Notes_Scope);
            Assert.Equal(partOIteration3Record.Stage_Refused, result.Stage_Refused);
            Assert.Equal(partOIteration3Record.IsComplete, result.IsComplete);
        }

        [Fact]
        public void A_binding_survives_including_its_dwelling_grouping_and_its_absent_extract_duty()
        {
            PartOIteration3Record partOIteration3Record = Record();

            PartOIteration3BindingRecord expected = Assert.Single(partOIteration3Record.Bindings);
            PartOIteration3BindingRecord result = Assert.Single(PartOIteration3Record.Parse(partOIteration3Record.ToString()).Bindings);

            Assert.Equal(expected.Guid_Space, result.Guid_Space);
            Assert.Equal(expected.Name_Space, result.Name_Space);
            Assert.Equal(expected.Guid_Dwelling, result.Guid_Dwelling);
            Assert.Equal(expected.Name_Dwelling, result.Name_Dwelling);
            Assert.Equal(expected.Guid_SystemSpace, result.Guid_SystemSpace);
            Assert.Equal(expected.Guid_AirSystem, result.Guid_AirSystem);
            Assert.Equal(expected.Reference_SystemZone, result.Reference_SystemZone);
            Assert.Equal(13.0, result.DesignFlowRate_Supply_Lps);

            //A room with no extract terminal has NO extract duty. Round-tripping it as zero would make it
            //a different room.
            Assert.Null(result.DesignFlowRate_Extract_Lps);
        }

        [Fact]
        public void A_file_record_survives_with_the_fingerprint_a_review_validates_by()
        {
            PartOIteration3Record partOIteration3Record = PartOIteration3Record.Parse(Record().ToString());

            PartOIteration3FileRecord result = partOIteration3Record.File(PartOIteration3Roles.Bridge_TSD);

            Assert.NotNull(result);
            Assert.Equal("C:\\out\\Flat-It3B-Bridge.tsd", result.Path);
            Assert.Equal(12345L, result.Length);
            Assert.Equal(638_100_000_000_000_000L, result.Ticks_Utc);
        }

        [Fact]
        public void The_ledger_survives_with_its_refusal_reasons_and_artifacts_verbatim()
        {
            PartOIteration3Record partOIteration3Record = PartOIteration3Record.Parse(Record().ToString());

            List<PartOIteration3StageState> stages = partOIteration3Record.Stages;

            Assert.Equal(14, stages.Count);

            Assert.Equal(PartOIteration3StageStatus.Completed, stages[0].Status);
            Assert.Equal(["C:\\out\\Flat-It3B.tbd (created)"], stages[0].Artifacts);

            Assert.Equal(PartOIteration3StageStatus.Refused, stages[2].Status);
            Assert.Equal(["Reference A could not be assessed."], stages[2].Reasons);

            Assert.All(stages.GetRange(3, 11), x => Assert.Equal(PartOIteration3StageStatus.NotRun, x.Status));
        }

        [Fact]
        public void Text_that_is_not_a_record_reads_as_none_rather_than_throwing()
        {
            Assert.Null(PartOIteration3Record.Parse(null));
            Assert.Null(PartOIteration3Record.Parse("   "));
            Assert.Null(PartOIteration3Record.Parse("{ this is not json"));
            Assert.Null(PartOIteration3Record.Parse("[1, 2, 3]"));
        }

        /// <summary>
        /// A record written by an older or newer build is refused rather than read optimistically: a
        /// field that moved would otherwise read as a default and validate.
        /// </summary>
        [Fact]
        public void A_record_of_another_schema_reads_back_stating_that_schema()
        {
            string text = Record().ToString().Replace(PartOIteration3Record.CurrentSchema, "PartOIteration3Record:v0");

            PartOIteration3Record result = PartOIteration3Record.Parse(text);

            Assert.NotNull(result);
            Assert.NotEqual(PartOIteration3Record.CurrentSchema, result.Schema);
        }

        /// <summary>The record a run writes is the record a review reads - through a real file.</summary>
        [Fact]
        public void A_record_written_to_disk_reads_back_identically()
        {
            string directory = PartOIteration3Fixture.Directory_Temp();

            try
            {
                string path = Path.Combine(directory, "Flat-Iteration3.json");

                string text = Record().ToString();

                File.WriteAllText(path, text);

                PartOIteration3Record result = Query.PartOIteration3PairingRecord(path);

                Assert.NotNull(result);
                Assert.Equal(text, result.ToString());
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
