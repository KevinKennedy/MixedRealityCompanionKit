using HoloLensCommander.Device;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace HoloLensCommander.Controls
{
    public sealed partial class JobQueueStatusControl : UserControl
    {
        public static readonly DependencyProperty JobQueueProperty =
            DependencyProperty.Register("JobQueue", typeof(JobQueue), typeof(JobQueueStatusControl), new PropertyMetadata(null, (o, args) => ((JobQueueStatusControl)o).OnJobQueueChanged((JobQueue)args.OldValue, (JobQueue)args.NewValue)));

        public JobQueue JobQueue
        {
            get { return (JobQueue)GetValue(JobQueueProperty); }
            set { SetValue(JobQueueProperty, value); }
        }

        public JobQueueStatusControl()
        {
            this.InitializeComponent();
        }

        private void OnJobQueueChanged(JobQueue oldValue, JobQueue newValue)
        {
            if(oldValue != null)
            {
               oldValue.JobStatusChanged -= this.JobQueueJobStatusChanged;
            }

            if(newValue != null)
            {
                newValue.JobStatusChanged += this.JobQueueJobStatusChanged;
            }

            this.UpdateFromJobQueue();
        }

        private void JobQueueJobStatusChanged(Job changedJob, JobStatus previousStatus, JobStatus newStatus, string statusMessage)
        {
            this.UpdateFromJobQueue();
        }

        private void UpdateFromJobQueue()
        {
            int outOfBandJobCount = 0;
            int regularJobCount = 0;

            var jobs = this.JobQueue.GetJobs();
            foreach (var job in jobs)
            {
                if (job.OutOfBand)
                {
                    outOfBandJobCount++;
                }
                else
                {
                    regularJobCount++;
                }
            }

            int newRowCount = (outOfBandJobCount > 0 ? outOfBandJobCount : 1) +
                    (regularJobCount > 0 ? regularJobCount : 1);

            this.MainGrid.Children.Clear();
            this.MainGrid.RowDefinitions.Clear();

            for (int rowIndex = 0; rowIndex < newRowCount; rowIndex++)
            {
                var newRow = new RowDefinition() { Height = new GridLength(1.0, GridUnitType.Star) };
                this.MainGrid.RowDefinitions.Add(newRow);
            }

            // Add the out-of-band jobs to the first rows
            int currentRowIndex = 0;
            foreach (var job in jobs)
            {
                if (job.OutOfBand)
                {
                    Grid rowGrid = new Grid();
                    Grid.SetRow(rowGrid, currentRowIndex);
                    this.MainGrid.Children.Add(rowGrid);

                    this.SetGridColumns(rowGrid, 2);
                    rowGrid.Children.Add(this.CreateJobVisual(currentRowIndex, 1, job));

                    currentRowIndex++;
                }
            }

            // Add the regular jobs to the last row
            Grid regularJobsRowGrid = new Grid();
            Grid.SetRow(regularJobsRowGrid, currentRowIndex);
            this.MainGrid.Children.Add(regularJobsRowGrid);
            this.SetGridColumns(regularJobsRowGrid, regularJobCount);
            int currentJobColumnIndex = regularJobCount - 1;
            foreach (var job in jobs)
            {
                if (!job.OutOfBand)
                {
                    regularJobsRowGrid.Children.Add(this.CreateJobVisual(newRowCount - 1, currentJobColumnIndex, job));

                    currentRowIndex++;
                    currentJobColumnIndex--;
                }
            }
        }

        private void SetGridColumns(Grid grid, int columnCount)
        {
            grid.ColumnDefinitions.Clear();
            for(int columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1.0, GridUnitType.Star) });
            }
        }

        private UIElement CreateJobVisual(int row, int column, Job job)
        {
            var visual = new Border()
            {
                BorderBrush = new SolidColorBrush() { Color = Colors.Black },
                BorderThickness = new Thickness(1.0),
                Background = new SolidColorBrush(Colors.Gray)
            };

            Grid.SetColumn(visual, column);
            Grid.SetRow(visual, row);
            ToolTipService.SetToolTip(visual, job.ToString());

            return visual;
        }

    }
}
