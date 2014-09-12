using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Timesheet
{
    public class ReportResultModel
    {
        public ReportResultModel()
        {
            ProjectIds = new int[0];
        }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public string StartDateString { get; set; }
        public string EndDateString { get; set; }
        public object Results { get; set; }
        public IEnumerable<int> ProjectIds { get; set; }
        public bool Flag { get; set; }
        public string FilterName { get; set; }
        public bool IncludeClosed { get; set; }
        public string FilterByClosedStatuses { get; set; }
       
        public IEnumerable<TimeReportModel> ResultsAsTimeReport
        {
            get { return Results as IEnumerable<TimeReportModel>; }
        }
        public string Title { get; set; }

        public Dictionary<int, string> Dictionary { get; set; }
    }
}
