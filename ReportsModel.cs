using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Countersoft.Foundation.Commons.Core;
using Countersoft.Gemini.Commons.Dto;
using Countersoft.Gemini.Infrastructure.Helpers;

namespace Timesheet
{
    public class ReportsModel
    {
        public ReportsModel()
        {
            ReportOptions = new ReportOptions();
        }

        public Dictionary<int, string> ReportMenu { get; set; }

        public enum ReportTypes
        {
            TimeReport = 50,
            TimeReportProject = 51,
            TimeReportUser = 53            
        }

        public ReportOptions ReportOptions { get; set; }

        public IEnumerable<SelectListItem> AllReports
        {
            get { return new SelectList(ReportMenu, "Key", "Value", ReportOptions.Reports); }
     
        }

        public IEnumerable<SelectListItem> FilterList { get; set; }

        public IEnumerable<SelectListItem> TimeList { get; set; }

        public IEnumerable<SelectListItem> ProjectList { get; set; }

        public IEnumerable<SelectListItem> ResourceList { get; set; }

        public IEnumerable<SelectListItem> GroupByList { get; set; }
    }

    public class ReportOptions
    {
        public ReportOptions()
        {
            Reports = 0;
            Parameters = new Dictionary<string, string>();           
            ProjectIds = new List<int>();
            ResourceIds = new List<int>();
            TimeTypeIds = new List<string>();
            StartDateString = string.Empty;
            EndDateString = string.Empty;
        }

        public int Reports { get; set; }
        public Dictionary<string, string> Parameters { get; set; }

        public DateTime? EndDate { get; set; }
        public string EndDateString { get; set; }
        public string EndDateDisplay
        {
            get
            {
                return EndDateString;
            }
        }

        public DateTime? StartDate { get; set; }
        public string StartDateString { get; set; }
        public string StartDateDisplay
        {
            get {
                return StartDateString;
            }
        }

        /*private string _subChart;
        public string SubChart
        {
            get { return _subChart; }
            set { _subChart = value; }
        }*/

        public bool? ProjectSummaryChart { get; set; }

        //public string PdfUrl { get; set; }

        //public string FieldId { get; set; }
        public int? FilterId { get; set; }

        public List<int> ProjectIds { get; set; }
        public List<int> ResourceIds { get; set; }
        public List<string> TimeTypeIds { get; set; }
        public int GroupBy { get; set; }

    }    
}
