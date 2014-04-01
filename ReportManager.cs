using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Countersoft.Gemini;
using Countersoft.Gemini.Commons;
using Countersoft.Gemini.Commons.Dto;
using Countersoft.Gemini.Commons.Entity;
using Countersoft.Gemini.Commons.Meta;
using Countersoft.Gemini.Contracts;
using Countersoft.Gemini.Contracts.Caching;
using Countersoft.Gemini.Infrastructure.Managers;
using System.Linq;
using Countersoft.Foundation.Commons.Extensions;
using System.Globalization;

namespace Timesheet
{
    public class ReportManager : BaseManager
    {
        public ReportManager(ICacheContainer cache, UserContext userContext, GeminiContext geminiContext)
            : base(cache, userContext, geminiContext)
        {
        }

        public static List<DayOfWeek> GetWeekends()
        {
            return new List<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday }; ;
        }

        public IEnumerable<TimeReportModel> GetTimeReport(ReportOptions reportOptions)
        {
            IssuesFilter filter = new IssuesFilter();
            if (reportOptions.ResourceIds.Count > 0)
                filter.TimeLoggedBy = IssuesFilter.CreateFilterList(reportOptions.ResourceIds);
            if (reportOptions.ProjectIds.Count > 0)
                filter.Projects = IssuesFilter.CreateFilterList(reportOptions.ProjectIds);
            if (reportOptions.StartDate != null)
                filter.TimeLoggedAfter = reportOptions.StartDate.Value.ToString();
            if (reportOptions.EndDate != null)
                filter.TimeLoggedBefore = reportOptions.EndDate.Value.ToString();

            filter.IncludeClosed = true;

            IssueManager manager = new IssueManager(this);
            List<IssueDto> issueDtos = manager.GetFiltered(filter);

            var start = reportOptions.StartDate == null ? new DateTime() : reportOptions.StartDate.Value.ClearTime();
            var end = reportOptions.EndDate == null ? DateTime.Today.AddDays(1) : reportOptions.EndDate.Value.AddDays(1).ClearTime();
            issueDtos.ForEach(i => i.TimeEntries.RemoveAll(t => t.Entity.EntryDate < start || t.Entity.EntryDate >= end));
            //Got all issues based on the filter, now need to do specifics
            switch (reportOptions.Reports)
            {
                case (int)ReportsModel.ReportTypes.TimeReportProject:
                case (int)ReportsModel.ReportTypes.TimeReportUser:
                    if (reportOptions.GroupBy == 1) // Resource
                        return GetTimeDateReport(issueDtos, reportOptions);
                    if (reportOptions.GroupBy == 2) // Issue
                        return GetTimeIssueReport(issueDtos, reportOptions);
                    else // Time Type
                        return GetTimeTypeReport(issueDtos, reportOptions);
            }

            return null;

        }

        private IEnumerable<TimeReportModel> GetTimeIssueReport(List<IssueDto> allissues, ReportOptions options)
        {
            var result = new List<TimeReportModel>();

            var date = options.StartDate.Value.ClearTime();

            if (options.TimeTypeIds.Count == 0) //if no type filter, show all
            {
                var timeTypes = GetTimeFilter(new List<string>(), options.ProjectIds).Select(x => string.Concat('|', x.Value)).ToList();
                timeTypes.Add("0|");
                options.TimeTypeIds = timeTypes.ToList();
            }
            List<string> types = new List<string>(options.TimeTypeIds);

            while (date <= options.EndDate)
            {
                var dayIssues = allissues
                    .Where(i => i.TimeEntries
                                    .Any(te => te.Entity.EntryDate.ClearTime() == date
                                               &&
                                               (options.TimeTypeIds.Count == 0 ||
                                                types.Find(t => t.Contains(string.Format("{0}|", te.Entity.TimeTypeId.GetValueOrDefault()), StringComparison.InvariantCultureIgnoreCase)) != null)));


                foreach (IssueDto issueDto in dayIssues)
                {
                    var issueItem = new TimeReportModel
                    {
                        Date = date,
                        Display = issueDto.Entity.Title,
                        Minutes = issueDto.TimeEntries
                            .Where(
                                te =>
                                te.Entity.EntryDate.ClearTime() == date &&
                                (options.ResourceIds.Count == 0 || options.ResourceIds.Contains(te.Entity.UserId)) &&
                                (options.TimeTypeIds.Count == 0 ||
                                 types.Find(t => t.Contains(string.Format("{0}|", te.Entity.TimeTypeId.GetValueOrDefault()), StringComparison.InvariantCultureIgnoreCase)) != null))
                            .Sum(te => te.Entity.Hours * 60 + te.Entity.Minutes),
                        KeyId = issueDto.Entity.Id,
                        SubItems = new Collection<TimeReportModel>(),
                        ReportItem = issueDto
                    };

                    foreach (var time in issueDto.TimeEntries
                                        .Where(te => te.Entity.EntryDate.ClearTime() == date
                                            && (options.ResourceIds.Count == 0 || !options.ResourceIds.Any() || options.ResourceIds.Contains(te.Entity.UserId))
                                            && (options.TimeTypeIds.Count == 0
                                                    || types.Find(t => t.Contains(string.Format("{0}|", te.Entity.TimeTypeId.GetValueOrDefault()), StringComparison.InvariantCultureIgnoreCase)) != null)))
                    {
                        var timeItem = new TimeReportModel
                        {
                            Date = date,
                            Display = time.Entity.Comment,
                            KeyId = time.Entity.Id,
                            Minutes = time.Entity.Hours * 60 + time.Entity.Minutes,
                            TimeTypeId = time.Entity.TimeTypeId.ToString(),
                            ReportItem = time
                        };
                        issueItem.SubItems.Add(timeItem);
                    }



                    result.Add(issueItem);
                }
                date = date.AddDays(1);
            }
            return result;
        }


        private IEnumerable<TimeReportModel> GetTimeDateReport(List<IssueDto> allissues, ReportOptions options)
        {
            var result = new List<TimeReportModel>();
            var usermanager = new UserManager(this);
            if (options.TimeTypeIds.Count == 0) //if no type filter, show all
            {
                var timeTypes = GetTimeFilter(new List<string>(), options.ProjectIds).Select(x => string.Concat('|', x.Value)).ToList();
                timeTypes.Add("0|");
                options.TimeTypeIds = timeTypes.ToList();
            }
            List<string> types = new List<string>(options.TimeTypeIds);
            IEnumerable<int> resources = allissues
                .SelectMany(i => i.TimeEntries
                                     .Where(
                                         te =>
                                         options.ResourceIds.Count == 0 || options.ResourceIds.Contains(te.Entity.UserId))
                                     .Select(te => te.Entity.UserId))
                .Distinct();

            foreach (int rId in resources)
            {
                var resourceId = rId;
                var date = options.StartDate.Value.ClearTime();


                var fullname = usermanager.Get(resourceId).Fullname;
                while (date <= options.EndDate)
                {
                    var dayIssues = allissues
                        .Where(i => i.TimeEntries
                                        .Any(te => te.Entity.EntryDate.ClearTime() == date
                                                   && te.Entity.UserId == resourceId
                                                   &&
                                                   (options.TimeTypeIds.Count == 0 ||
                                                    types.Find(t => t.Contains(string.Format("{0}|", te.Entity.TimeTypeId.GetValueOrDefault()), StringComparison.InvariantCultureIgnoreCase)) != null)));
                    var resourceItem = new TimeReportModel
                    {
                        Date = date,
                        Display = fullname,
                        Minutes = dayIssues
                            .SelectMany(i => i.TimeEntries)
                            .Where(te => te.Entity.UserId == resourceId
                                         && te.Entity.EntryDate.ClearTime() == date
                                         &&
                                         (options.TimeTypeIds.Count == 0 ||
                                          types.Find(t => t.Contains(string.Format("{0}|", te.Entity.TimeTypeId.GetValueOrDefault()), StringComparison.InvariantCultureIgnoreCase)) != null))
                            .Sum(te => te.Entity.Hours * 60 + te.Entity.Minutes),
                        SubItems = new Collection<TimeReportModel>(),
                        KeyId = resourceId,
                        ReportItem = Cache.Users.Get(resourceId).Fullname
                    };

                    foreach (IssueDto issueDto in dayIssues)
                    {
                        var issueItem = new TimeReportModel
                        {
                            Date = date,
                            Display = issueDto.Entity.Title,
                            Minutes = issueDto.TimeEntries
                                .Where(
                                    te =>
                                    te.Entity.EntryDate.ClearTime() == date &&
                                    te.Entity.UserId == resourceId
                                    &&
                                    (options.TimeTypeIds.Count == 0 ||
                                     types.Find(t => t.Contains(string.Format("{0}|", te.Entity.TimeTypeId.GetValueOrDefault()), StringComparison.InvariantCultureIgnoreCase)) != null))
                                .Sum(te => te.Entity.Hours * 60 + te.Entity.Minutes),
                            KeyId = issueDto.Entity.Id,
                            SubItems = new Collection<TimeReportModel>(),
                            ReportItem = issueDto
                        };

                        foreach (var time in issueDto.TimeEntries
                                            .Where(te => te.Entity.UserId == resourceId
                                                     && te.Entity.EntryDate.ClearTime() == date
                                                     && (options.TimeTypeIds.Count == 0
                                                        ||
                                                        types.Find(t => t.Contains(string.Format("{0}|", te.Entity.TimeTypeId.GetValueOrDefault()), StringComparison.InvariantCultureIgnoreCase)) != null)))
                        {
                            var timeItem = new TimeReportModel
                            {
                                Date = date,
                                Display = time.Entity.Comment,
                                KeyId = time.Entity.Id,
                                Minutes = time.Entity.Hours * 60 + time.Entity.Minutes,
                                TimeTypeId = time.Entity.TimeTypeId.ToString(),
                                ReportItem = time
                            };
                            issueItem.SubItems.Add(timeItem);
                        }
                        resourceItem.SubItems.Add(issueItem);
                    }
                    date = date.AddDays(1);
                    result.Add(resourceItem);
                }
            }
            return result;
        }

        private IEnumerable<TimeReportModel> GetTimeTypeReport(List<IssueDto> allissues, ReportOptions options)
        {
            var result = new List<TimeReportModel>();
            var usermanager = new UserManager(this);

            //TODO get time types for all projects involved.
            if (options.TimeTypeIds.Count == 0) //if no type filter, show all
                options.TimeTypeIds = GetTimeFilter(new List<string>(), options.ProjectIds).Select(x => x.Value).ToList();

            IEnumerable<int> resources = allissues
                    .SelectMany(i => i.TimeEntries
                            .Where(te => options.ResourceIds.Count == 0 || options.ResourceIds.Contains(te.Entity.UserId))
                            .Select(te => te.Entity.UserId))
                    .Distinct();


            if (!resources.Any())
            {
                result.AddRange(options.
                    TimeTypeIds.Select(i => new TimeReportModel
                    {
                        Date = null,
                        Display = GetResource(ResourceKeys.NoData),
                        Minutes = 0,
                        SubItems = new Collection<TimeReportModel>(),
                        KeyId = 0,
                        TimeTypeId = i,
                        ReportItem = null
                    }));
            }

            foreach (int rId in resources)
            {
                var resourceId = rId;

                var fullname = usermanager.Get(resourceId).Fullname;

                //Get all unassigned items, before looping through time types
                var timeTypeIssues = allissues
                    .Where(i => i.TimeEntries
                                    .Any(te => te.Entity.TimeTypeId == null
                                               && te.Entity.UserId == resourceId));
                var resourceItem = new TimeReportModel
                {
                    Date = null,
                    Display = fullname,
                    Minutes = timeTypeIssues
                        .SelectMany(i => i.TimeEntries)
                        .Where(te => te.Entity.UserId == resourceId
                                     && te.Entity.TimeTypeId == null)
                        .Sum(te => te.Entity.Hours * 60 + te.Entity.Minutes),
                    SubItems = new Collection<TimeReportModel>(),
                    KeyId = resourceId,
                    TimeTypeId = string.Empty,
                    ReportItem = Cache.Users.Get(resourceId)
                };
                //Get Issues for the resource
                foreach (IssueDto issueDto in timeTypeIssues)
                {
                    var issueItem = new TimeReportModel
                    {
                        Date = null,
                        Display = issueDto.Entity.Title,
                        Minutes = issueDto.TimeEntries
                            .Where(te => te.Entity.UserId == resourceId
                                && te.Entity.TimeTypeId == null)
                            .Sum(te => te.Entity.Hours * 60 + te.Entity.Minutes),
                        KeyId = issueDto.Entity.Id,
                        TimeTypeId = string.Empty,
                        SubItems = new Collection<TimeReportModel>(),
                        ReportItem = issueDto
                    };

                    foreach (var time in issueDto.TimeEntries
                                                .Where(te => te.Entity.UserId == resourceId
                                                    && te.Entity.TimeTypeId == null))
                    {
                        var timeItem = new TimeReportModel
                        {
                            Date = time.Entity.EntryDate,
                            TimeTypeId = string.Empty,
                            Display = time.Entity.Comment,
                            KeyId = time.Entity.Id,
                            Minutes = time.Entity.Hours * 60 + time.Entity.Minutes,
                            ReportItem = time
                        };
                        issueItem.SubItems.Add(timeItem);
                    }
                    resourceItem.SubItems.Add(issueItem);
                }
                result.Add(resourceItem);

                foreach (var id in options.TimeTypeIds)
                {
                    var timeTypeIds = id.SplitEntries(0);
                    timeTypeIssues = allissues
                        .Where(i => i.TimeEntries
                                        .Any(te => timeTypeIds.Contains(te.Entity.TimeTypeId.GetValueOrDefault())
                                                   && te.Entity.UserId == resourceId));
                    resourceItem = new TimeReportModel
                    {
                        Date = null,
                        Display = fullname,
                        Minutes = timeTypeIssues
                            .SelectMany(i => i.TimeEntries)
                            .Where(te => te.Entity.UserId == resourceId
                                         && timeTypeIds.Contains(te.Entity.TimeTypeId.GetValueOrDefault()))
                            .Sum(te => te.Entity.Hours * 60 + te.Entity.Minutes),
                        SubItems = new Collection<TimeReportModel>(),
                        KeyId = resourceId,
                        TimeTypeId = id,
                        ReportItem = Cache.Users.Get(resourceId)
                    };
                    //Get Issues for the resource
                    foreach (IssueDto issueDto in timeTypeIssues)
                    {
                        var issueItem = new TimeReportModel
                        {
                            Date = null,
                            Display = issueDto.Entity.Title,
                            Minutes = issueDto.TimeEntries
                                .Where(te => te.Entity.UserId == resourceId
                                    && timeTypeIds.Contains(te.Entity.TimeTypeId.GetValueOrDefault()))
                                .Sum(te => te.Entity.Hours * 60 + te.Entity.Minutes),
                            KeyId = issueDto.Entity.Id,
                            TimeTypeId = id,
                            SubItems = new Collection<TimeReportModel>(),
                            ReportItem = issueDto
                        };

                        foreach (var time in issueDto.TimeEntries
                                                    .Where(te => te.Entity.UserId == resourceId
                                                        && timeTypeIds.Contains(te.Entity.TimeTypeId.GetValueOrDefault())))
                        {
                            var timeItem = new TimeReportModel
                            {
                                Date = time.Entity.EntryDate,
                                TimeTypeId = time.Entity.TimeTypeId.ToString(),
                                Display = time.Entity.Comment,
                                KeyId = time.Entity.Id,
                                Minutes = time.Entity.Hours * 60 + time.Entity.Minutes,
                                ReportItem = time
                            };
                            issueItem.SubItems.Add(timeItem);
                        }
                        resourceItem.SubItems.Add(issueItem);
                    }
                    result.Add(resourceItem);
                }

            }
            return result;
        }

        public MultiSelectList GetTimeFilter(List<string> selected, List<int> projectIds)
        {
            var manager = new MetaManager(GeminiApp.Cache(), UserContext, GeminiContext);
            var projectManager = new ProjectManager(this);

            var projects = projectManager.Get(new List<int>(projectIds));
            var templateid = 0;
            List<TimeType> Timelist = new List<TimeType>();
            foreach (ProjectDto project in projects)
            {
                var template = GeminiContext.Projects.Get(project.Entity.Id);
                if (template != null)
                {
                    templateid = template.TemplateId;
                    List<TimeType> timeTypes = timeTypes = manager.TimeTypeGetAll(templateid);
                    //Timelist.AddRange(timeTypes);
                    foreach (var type in timeTypes)
                    {
                        var tag = string.Concat(type.Id, '|');
                        var current = Timelist.Find(t => string.Compare(t.Label, type.Label, StringComparison.InvariantCultureIgnoreCase) == 0);
                        if (current == null)
                        {
                            type.Tag = tag;
                            Timelist.Add(type);
                        }
                        else
                        {
                            if (!current.Tag.Contains(string.Concat('|', tag)))
                            {
                                current.Tag = string.Concat(current.Tag, tag);
                            }
                        }
                    }
                }
            }

            List<string> selectedItemsFormatted = new List<string>();

            foreach (var select in selected)
            {
                string item = select;

                List<string> splitTime = select.SplitEntries("|");

                /*if (select.StartsWith("|"))
                {
                    item = select.Remove(0, 1);
                }*/
                //splitTime.Contains(string.Format("{0}|",s.Id)
                var time = Timelist.Find(s =>  splitTime.Contains(s.Id.ToString()));

                if (time != null)
                {
                    item = time.Tag;
                }

                selectedItemsFormatted.Add(item);
            }

            return new MultiSelectList(Timelist, "Tag", "Label", selectedItemsFormatted);
        }

        public IEnumerable<SelectListItem> GetReourceFilter(List<int> selectedResources, List<int> projectIds)
        {
            var manager = new UserManager(GeminiApp.Cache(), UserContext, GeminiContext);

            List<User> projectResources = manager.GetProjectResources(projectIds.ToList()).Select(p => p.Entity).ToList();
            //projectResources.Insert(0, new User{Id = 0, Firstname = GetResource(ResourceKeys.Select)});
            var result = new Collection<SelectListItem>();
            //(projectResources, "id", "Fullname", selected);
            foreach (var projectResource in projectResources)
            {
                var item = new SelectListItem { Text = projectResource.Fullname, Value = projectResource.Id.ToString(CultureInfo.InvariantCulture) };
                if (selectedResources != null) item.Selected = selectedResources.Contains(projectResource.Id);
                result.Add(item);
            }

            return result;
        }
    }
}
