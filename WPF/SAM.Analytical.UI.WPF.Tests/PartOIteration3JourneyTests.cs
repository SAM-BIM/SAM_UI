// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using SAM.Analytical.UI;
using SAM.Core;
using SAM.Core.Tas;
using SAM.Weather;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// The Hub's Iteration 3 journey: what it says and does where the product method is missing a STEP
    /// (no saved cooling control rooms), as opposed to a FAULT (an invalid saved room, which still refuses).
    /// Nothing here touches TAS, Direct T3D or the simulation engine - it is presentation and routing.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class PartOIteration3JourneyTests
    {
        /// <summary>A prepared reference run whose prepared design carries NO saved cooling control rooms.</summary>
        private static (PartOIteration3RoomBindingTests Host, PartORun Run, VentilationUnitCatalogue Catalogue, PartOIteration3Eligibility Eligibility) WithoutSavedRooms()
        {
            PartOIteration3RoomBindingTests host = new();
            var test = host.Prepared();

            test.Run.AnalyticalModel_Prepared.RemoveValue(Analytical.AnalyticalModelParameter.PartODwellingStrategies);

            PartOIteration3Eligibility eligibility = Query.PartOIteration3Eligibility(test.Run, test.Run.IsAssessable(out string refusal), refusal);
            Assert.True(eligibility.CanRun, eligibility.Refusal_Run);

            return (host, test.Run, test.Catalogue, eligibility);
        }

        private static AnalyticalModel Dirty()
        {
            AnalyticalModel baseline = PartOMixedDesignFixture.Baseline();
            AdjacencyCluster adjacencyCluster = baseline.AdjacencyCluster;
            Space space = adjacencyCluster.GetSpaces().First();
            SpaceSimulationResult spaceSimulationResult = new(space.Name, "Tas", space.Guid.ToString());
            adjacencyCluster.AddObject(spaceSimulationResult);
            adjacencyCluster.AddRelation(space, spaceSimulationResult);

            return new AnalyticalModel(baseline, adjacencyCluster);
        }

        //---------------------------------------------------------------------------------------------
        //The steps
        //---------------------------------------------------------------------------------------------

        [Fact]
        public void Advice_OnACleanModelWithNoSavedRooms_OffersMixedDesignAndThenIteration2()
        {
            string advice = Query.PartOIteration3ProductMethodAdvice(PartOMixedDesignFixture.Baseline(), null, out bool offerMixedDesign);

            Assert.True(offerMixedDesign);
            Assert.Contains("1. Choose a cooling control room for each dwelling in Mixed Design", advice);
            Assert.Contains("Save selection", advice);
            Assert.Contains("2. Choose Iteration 2", advice);
            Assert.DoesNotContain("Remove Results", advice);
        }

        [Fact]
        public void Advice_OnAModelThatCarriesResults_SendsThePersonToRemoveResultsFirst_AndDoesNotOfferMixedDesign()
        {
            string advice = Query.PartOIteration3ProductMethodAdvice(Dirty(), null, out bool offerMixedDesign);

            Assert.False(offerMixedDesign);
            Assert.Contains("1. Save a copy of the model without its simulation results", advice);
            Assert.Contains("2. Choose a cooling control room", advice);
            Assert.Contains("3. Choose Iteration 2", advice);
        }

        [Fact]
        public void Advice_WhenTheRoomsAreSavedOnTheModelButNotInTheReference_OnlyAsksForTheRunAgain()
        {
            AnalyticalModel saved = PartOMixedDesignFixture.WithStrategies(PartOMixedDesignFixture.Baseline(), PartOMixedDesignFixture.Mvhr);
            Assert.True(saved.GetValue<PartODwellingStrategySet>(Analytical.AnalyticalModelParameter.PartODwellingStrategies)?.IsValid);

            string advice = Query.PartOIteration3ProductMethodAdvice(saved, null, out bool offerMixedDesign);

            Assert.False(offerMixedDesign);
            Assert.StartsWith("This method needs the cooling control rooms saved on the model to be part of the reference case", advice);
            Assert.Contains("1. The cooling control rooms are saved on the model.", advice);
            Assert.Contains("2. Choose Iteration 2", advice);
        }

        [Fact]
        public void Advice_ForAnIteration2Reference_AsksForPrepareAndRunAgain_NotForIteration2()
        {
            List<Zone> zones = PartOMixedDesignFixture.Dwellings(PartOMixedDesignFixture.Baseline());
            PartORun partORun = new();
            Assert.True(partORun.Prepare(PartOMixedDesignFixture.Baseline(), PartOIteration3Fixture.Scenarios(),
                new PartOPreparationContext(PartOIteration.BasePassive, zones, null, [PartOMixedCoolingTests.Descriptor]), []));

            string advice = Query.PartOIteration3ProductMethodAdvice(PartOMixedDesignFixture.Baseline(), partORun, out _);

            Assert.Contains("Select Prepare & Run for Iteration 2 again", advice);
            Assert.DoesNotContain("Choose Iteration 2", advice);
        }

        //---------------------------------------------------------------------------------------------
        //The Hub
        //---------------------------------------------------------------------------------------------

        [WpfFact]
        public void MissingRooms_SelectAMethodThatCanRun_SayWhy_AndDoNotCarryThatChoiceOver()
        {
            var test = WithoutSavedRooms();
            using (test.Host)
            {
                PartOWorkflowWindow window = new()
                {
                    AnalyticalModel = PartOMixedDesignFixture.Baseline(),
                    PartORun = test.Run,
                    VentilationUnitCatalogue = test.Catalogue,
                    Iteration3Eligibility = test.Eligibility,
                    Iteration3Mode = PartOIteration3BehaviourMode.SelectedProductManufacturerGuidance,
                };
                window.CompleteInitialisation();

                //The default cannot run - and it is a missing step - so the first method that can is selected.
                Assert.True(window.Iteration3MethodIsAutomatic);
                Assert.NotEqual(PartOIteration3BehaviourMode.SelectedProductManufacturerGuidance, window.Iteration3EffectiveMode);
                Assert.True(window.CanRunIteration3);
                Assert.Equal(1, window.Iteration3CheckedCount);
                Assert.True(string.IsNullOrEmpty(window.Iteration3RefusalText));
                Assert.Equal(PartOIteration3BehaviourMode.Parity, window.Iteration3Mode);

                //Product methods are tried before validation methods; in this fixture the certified-efficiency method
                //refuses too (its unit states no certified data), so the route check is what can run.
                Assert.Equal(PartOIteration3BehaviourMode.Parity, window.Iteration3EffectiveMode);

                //The notice names both methods, says why, and the Mixed Design step is on offer.
                Assert.Contains("Selected product — manufacturer operating guidance", window.Iteration3AutomaticText);
                Assert.Contains("Route check — design airflows, no product", window.Iteration3AutomaticText);
                Assert.Contains("listed below", window.Iteration3AutomaticText);

                //The steps for the method that cannot run yet are shown, calmly, with the Mixed Design step offered.
                Assert.Contains("cooling control room", window.Iteration3StepsText);
                Assert.True(window.Iteration3MixedDesignOffered);
                Assert.False(window.Iteration3RemoveResultsOffered);

                //Run and Open result act on the method that is on screen ...
                Assert.Equal(PartOIteration3BehaviourMode.Parity, window.Iteration3Mode);

                //... but only a method a person chose is carried: the next showing starts from the default and decides again.
                Assert.Equal(PartOIteration3BehaviourMode.SelectedProductManufacturerGuidance, window.Iteration3ModeCarried);
            }
        }

        [WpfFact]
        public void AChosenMethod_IsNeverReplaced_AndKeepsItsRefusalAndTheMixedDesignStep()
        {
            var test = WithoutSavedRooms();
            using (test.Host)
            {
                PartOWorkflowWindow window = new()
                {
                    AnalyticalModel = PartOMixedDesignFixture.Baseline(),
                    PartORun = test.Run,
                    VentilationUnitCatalogue = test.Catalogue,
                    Iteration3Eligibility = test.Eligibility,
                };
                window.CompleteInitialisation();
                Assert.True(window.Iteration3MethodIsAutomatic);

                //A person picks the default method themselves.
                Dictionary<PartOIteration3BehaviourMode, RadioButton> radioButtons = (Dictionary<PartOIteration3BehaviourMode, RadioButton>)typeof(PartOWorkflowWindow)
                    .GetField("radioButtons_Method", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
                radioButtons[PartOIteration3BehaviourMode.SelectedProductManufacturerGuidance].IsChecked = true;

                Assert.False(window.Iteration3MethodIsAutomatic);
                Assert.Equal(PartOIteration3BehaviourMode.SelectedProductManufacturerGuidance, window.Iteration3EffectiveMode);
                Assert.False(window.CanRunIteration3);
                Assert.True(string.IsNullOrEmpty(window.Iteration3RefusalText));
                Assert.Contains("Mixed Design", window.Iteration3StepsText);
                Assert.Contains("Save selection", window.Iteration3StepsText);
                Assert.True(window.Iteration3MixedDesignOffered);
                Assert.Equal(string.Empty, window.Iteration3AutomaticText);
            }
        }

        [WpfFact]
        public void OnAModelThatCarriesResults_TheFirstStepIsRemoveResults_AndMixedDesignIsNotOffered()
        {
            var test = WithoutSavedRooms();
            using (test.Host)
            {
                PartOWorkflowWindow window = new()
                {
                    AnalyticalModel = Dirty(),
                    PartORun = test.Run,
                    VentilationUnitCatalogue = test.Catalogue,
                    Iteration3Eligibility = test.Eligibility,
                };
                window.CompleteInitialisation();

                Assert.True(window.Iteration3RemoveResultsOffered);
                Assert.False(window.Iteration3MixedDesignOffered);
                Assert.Contains("Save a copy of the model without its simulation results", window.Iteration3StepsText);

                //Only the current step carries a button, and it is the first.
                Assert.Contains("\u2192 1.", window.Iteration3StepsText);
                Assert.Contains("\u25CB 2.", window.Iteration3StepsText);
            }
        }

        [Fact]
        public void ACompletedRunsNotes_AreKeptOnTheHubLine_NotInABox()
        {
            PartOSimulationOutcome partOSimulationOutcome = new();
            partOSimulationOutcome.Notes.Add("Pre-simulation check (warning): Studio 1_0 Space ...");
            partOSimulationOutcome.Notes.Add("Pre-simulation check (warning): Bathroom_2 Space ...");

            string notes = Modify.NotesText(partOSimulationOutcome);
            Assert.Contains("Studio 1_0", notes);

            PartOWorkflowOutcome partOWorkflowOutcome = Modify.CompletedOutcome("Iteration 1a", TimeSpan.FromMinutes(1), null, 2, notes);

            Assert.Contains("2 notes", partOWorkflowOutcome.Detail);
            Assert.DoesNotContain("shown", partOWorkflowOutcome.Detail);
            Assert.Contains("Notes from the simulation", partOWorkflowOutcome.ToolTip);
            Assert.Contains("Bathroom_2", partOWorkflowOutcome.ToolTip);

            Assert.Null(Modify.NotesText(new PartOSimulationOutcome()));
        }

        [WpfFact]
        public void ACarriedNonDefaultMethod_IsAChoice_AndIsNotReplaced()
        {
            var test = WithoutSavedRooms();
            using (test.Host)
            {
                PartOWorkflowWindow window = new()
                {
                    PartORun = test.Run,
                    VentilationUnitCatalogue = test.Catalogue,
                    Iteration3Eligibility = test.Eligibility,
                    Iteration3Mode = PartOIteration3BehaviourMode.Parity,
                };
                window.CompleteInitialisation();

                Assert.False(window.Iteration3MethodIsAutomatic);
                Assert.Equal(PartOIteration3BehaviourMode.Parity, window.Iteration3EffectiveMode);
                Assert.Equal(PartOIteration3BehaviourMode.Parity, window.Iteration3Mode);
            }
        }

        [WpfFact]
        public void WhereTheDefaultCanRun_NothingIsAutomatic_AndNoMixedDesignStepIsOffered()
        {
            using PartOIteration3RoomBindingTests host = new();
            var test = host.Prepared();
            PartOIteration3Eligibility eligibility = Query.PartOIteration3Eligibility(test.Run, test.Run.IsAssessable(out string refusal), refusal);

            PartOWorkflowWindow window = new()
            {
                PartORun = test.Run,
                VentilationUnitCatalogue = test.Catalogue,
                Iteration3Eligibility = eligibility,
            };
            window.CompleteInitialisation();

            Assert.False(window.Iteration3MethodIsAutomatic);
            Assert.Equal(PartOIteration3BehaviourMode.SelectedProductManufacturerGuidance, window.Iteration3EffectiveMode);
            Assert.True(window.CanRunIteration3);
            Assert.Equal(string.Empty, window.Iteration3AutomaticText);
            Assert.False(window.Iteration3MixedDesignOffered);
        }

        [WpfFact]
        public void TheJumpToIteration3_IsOfferedOnlyWhereThePanelIs()
        {
            PartOWorkflowWindow without = new();
            without.CompleteInitialisation();
            Assert.False(without.GoToIteration3Offered);

            using PartOIteration3RoomBindingTests host = new();
            var test = host.Prepared();

            PartOWorkflowWindow with = new()
            {
                PartORun = test.Run,
                VentilationUnitCatalogue = test.Catalogue,
                Iteration3Eligibility = Query.PartOIteration3Eligibility(test.Run, test.Run.IsAssessable(out string refusal), refusal),
            };
            with.CompleteInitialisation();
            Assert.True(with.GoToIteration3Offered);
        }

        [WpfFact]
        public void TheMixedDesignButton_ClosesTheHubWithTheMixedDesignAction()
        {
            var test = WithoutSavedRooms();
            using (test.Host)
            {
                PartOWorkflowWindow window = new()
                {
                    AnalyticalModel = PartOMixedDesignFixture.Baseline(),
                    PartORun = test.Run,
                    VentilationUnitCatalogue = test.Catalogue,
                    Iteration3Eligibility = test.Eligibility,
                    ShowInTaskbar = false,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -20000,
                };
                window.CompleteInitialisation();
                Assert.True(window.Iteration3MixedDesignOffered);

                Button button = (Button)window.FindName("button_It3MixedDesign");
                window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new System.Action(() => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));

                Assert.True(window.ShowDialog());
                Assert.Equal(PartOWorkflowAction.MixedDesign, window.Action);
            }
        }

        [Fact]
        public void AfterMixedDesignSavesRooms_TheHubLineStatesTheNextStep_WhateverTheRunNowIs()
        {
            PartOWorkflowOutcome partOWorkflowOutcome = Modify.MixedDesignSavedOutcome();

            Assert.Equal(PartOWorkflowOutcomeKind.Information, partOWorkflowOutcome.Kind);
            Assert.Contains("Cooling control rooms saved", partOWorkflowOutcome.Headline);
            Assert.Contains("Iteration 2", partOWorkflowOutcome.Detail);

            //It claims nothing about the run, so it holds whether or not the model change dropped it.
            Assert.Same(partOWorkflowOutcome, Modify.HubOutcome(partOWorkflowOutcome, new PartORun(), null));
        }

        [WpfFact]
        public void TheDirectT3DBox_FollowsEveryChange_NotOnlyAClick()
        {
            WeatherData weatherData = new("Test", "Test", 51.1, -0.2, 60);

            PartOWorkflowWindow window = new()
            {
                SimulationCase = new PartOSimulationCase { WeatherData = weatherData, OutputDirectory = System.IO.Path.GetTempPath() },
            };
            window.CompleteInitialisation();

            CheckBox checkBox = (CheckBox)window.FindName("checkBox_DirectT3D");
            Assert.DoesNotContain("Direct T3D", window.SimulationCaseSummary);

            //A change that raises no click - the keyboard, or UI Automation's Toggle pattern.
            checkBox.IsChecked = true;
            Assert.Contains("Direct T3D", window.SimulationCaseSummary);
            Assert.True(window.SimulationCase.DirectT3D);

            checkBox.IsChecked = false;
            Assert.DoesNotContain("Direct T3D", window.SimulationCaseSummary);
            Assert.False(window.SimulationCase.DirectT3D);
        }
    }
}
