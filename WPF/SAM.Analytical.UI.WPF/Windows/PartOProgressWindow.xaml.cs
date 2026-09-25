// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// The one progress window of a long Part O operation - see <see cref="PartOProgressHost"/>, which owns
    /// its thread. It reads <see cref="PartOProgressState"/> on a timer, so the thread doing the work never has
    /// to reach it, and a stage the work announces appears within half a second.
    /// </summary>
    public partial class PartOProgressWindow : System.Windows.Window
    {
        private readonly DispatcherTimer dispatcherTimer;

        private PartOProgressState partOProgressState;

        private bool cancellable = true;

        private bool cancelRequested;

        public PartOProgressWindow()
        {
            InitializeComponent();

            dispatcherTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(500),
            };

            dispatcherTimer.Tick += (s, e) => Render();

            Loaded += (s, e) =>
            {
                Render();
                dispatcherTimer.Start();
            };

            Closed += (s, e) => dispatcherTimer.Stop();
        }

        /// <summary>Raised on this window's thread when Cancel is pressed. Once.</summary>
        public event EventHandler CancelRequested;

        public string Heading
        {
            get => textBlock_Heading.Text;
            set => textBlock_Heading.Text = value;
        }

        public string Subheading
        {
            get => textBlock_Subheading.Text;
            set
            {
                textBlock_Subheading.Text = value;
                textBlock_Subheading.Visibility = string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        /// <summary>
        /// Shows Cancel. Either way the note stays, because it also answers whether the operation can be
        /// stopped at all - "It cannot be cancelled" is information, not an absence.
        /// </summary>
        public bool Cancellable
        {
            get => cancellable;
            set
            {
                cancellable = value;
                button_Cancel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                Render();
            }
        }

        public PartOProgressState State
        {
            get => partOProgressState;
            set
            {
                partOProgressState = value;
                Render();
            }
        }

        /// <summary>Re-reads the state. Public so a test can render a window without waiting on its timer.</summary>
        public void Render()
        {
            if (partOProgressState is null)
            {
                return;
            }

            List<Row> rows = [];

            for (int i = 0; i < partOProgressState.Count; i++)
            {
                PartOProgressStageStatus partOProgressStageStatus = partOProgressState.Status(i);
                TimeSpan? duration = partOProgressState.Duration(i);

                rows.Add(new Row(
                    PartOProgressState.Glyph(partOProgressStageStatus),
                    partOProgressState.Label(i),
                    duration.HasValue && partOProgressStageStatus != PartOProgressStageStatus.Pending && partOProgressStageStatus != PartOProgressStageStatus.Skipped ? PartOProgressState.Format(duration.Value) : string.Empty,
                    partOProgressState.AccessibleLine(i, false),
                    partOProgressStageStatus));
            }

            itemsControl_Stages.ItemsSource = rows;

            string detail = partOProgressState.Detail;
            textBlock_Detail.Text = detail ?? string.Empty;
            textBlock_Detail.Visibility = string.IsNullOrWhiteSpace(detail) ? Visibility.Collapsed : Visibility.Visible;

            //Determinate only on a real count; otherwise the bar moves without claiming a position.
            double? fraction = partOProgressState.Fraction;
            progressBar.IsIndeterminate = !fraction.HasValue;
            progressBar.Value = fraction ?? 0;

            string percent = partOProgressState.Percent;
            textBlock_Percent.Text = percent ?? string.Empty;
            textBlock_Percent.Visibility = percent is null ? Visibility.Collapsed : Visibility.Visible;

            textBlock_Elapsed.Text = partOProgressState.ElapsedText;

            textBlock_Note.Text = PartOProgressState.Note(fraction.HasValue, cancellable, cancelRequested);
        }

        private void button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (cancelRequested)
            {
                return;
            }

            cancelRequested = true;

            button_Cancel.IsEnabled = false;
            button_Cancel.Content = "Cancelling…";

            Render();

            CancelRequested?.Invoke(this, EventArgs.Empty);
        }

        private sealed class Row
        {
            internal Row(string glyph, string name, string duration, string accessibleName, PartOProgressStageStatus partOProgressStageStatus)
            {
                Glyph = glyph;
                Name = name;
                Duration = duration;
                AccessibleName = accessibleName;
                StatusText = PartOProgressState.StatusText(partOProgressStageStatus);

                Foreground = partOProgressStageStatus switch
                {
                    PartOProgressStageStatus.Completed => Brushes.SeaGreen,
                    PartOProgressStageStatus.Running => Brushes.RoyalBlue,
                    PartOProgressStageStatus.Failed => Brushes.Firebrick,
                    _ => Brushes.Gray,
                };

                FontWeight = partOProgressStageStatus == PartOProgressStageStatus.Running ? FontWeights.SemiBold : FontWeights.Normal;
            }

            public string Glyph { get; }

            public string Name { get; }

            public string Duration { get; }

            public string AccessibleName { get; }

            public string StatusText { get; }

            public Brush Foreground { get; }

            public FontWeight FontWeight { get; }

            //What the ItemsControl's item peer is named by.
            public override string ToString()
            {
                return AccessibleName;
            }
        }
    }
}
