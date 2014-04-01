using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Timesheet
{    
    public class TimeReportModel
    {
        public string Display { get; set; }
        public DateTime? Date { get; set; }
        public string TimeTypeId { get; set; }
        public int Minutes { get; set; }

        public string DisplayTime { get { return string.Format("{0}h{1: #00}m", Minutes / 60, Minutes % 60); } }

        /// <summary>
        /// What ever the graph is grouped by
        /// </summary>
        public int KeyId { get; set; }

        public object ReportItem { get; set; }

        public ICollection<TimeReportModel> SubItems { get; set; }
    }    
}
