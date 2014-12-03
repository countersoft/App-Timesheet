using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.UI;
using Countersoft.Foundation.Commons.Extensions;
using Countersoft.Gemini;
using Countersoft.Gemini.Extensibility;
using Countersoft.Gemini.Commons;
using Countersoft.Gemini.Commons.Dto;
using Countersoft.Gemini.Commons.Entity;
using Countersoft.Gemini.Commons.Permissions;
using Countersoft.Gemini.Extensibility.Apps;
using Countersoft.Gemini.Infrastructure;
using Countersoft.Gemini.Infrastructure.Apps;
using Countersoft.Gemini.Infrastructure.Managers;
using System.Linq;
using System.Collections.ObjectModel;
using Countersoft.Gemini.Infrastructure.Helpers;
using Countersoft.Foundation.Commons.Core;

namespace Timesheet
{
       // This controller will consume the ajax posts from the view item and main config pages. See routes class at the end of this file
    [AppType(AppTypeEnum.FullPage),
    AppGuid("A95F5FEC-1477-4909-AF13-8E600383B03A"),
    AppControlGuid("32D3943A-F166-44A6-A9AF-5063302AC55A"),
    AppAuthor("Countersoft"), AppKey("Timesheet"),
    AppName("Timesheet"), AppDescription("Timesheet"),
    AppControlUrl("view"),
    AppRequiresViewPermission(true)]
    [OutputCache(Duration = 0, NoStore = true, Location = System.Web.UI.OutputCacheLocation.None)]
    public class TimesheetController : BaseAppController
    {
        private DateTime _defaultActivityDate = DateTime.Now.AddMonths(-3);
        private IEnumerable<SelectListItem> _timeTypes;
        private IEnumerable<SelectListItem> _resources;

        public override WidgetResult Caption(IssueDto issue = null)
        {
            return new WidgetResult() { Success = true, Markup = new WidgetMarkup(AppName) };
        }

        public override WidgetResult Show(IssueDto issue = null)
        {
            var model = new ReportsModel();

            model.ReportMenu = new Dictionary<int, string>
                                   {                                     
                                       {(int) ReportsModel.ReportTypes.TimeReportProject,  GetResource(ResourceKeys.TimeReportProjectName)},
                                       {(int) ReportsModel.ReportTypes.TimeReportUser,     GetResource(ResourceKeys.TimeReportUserName )},
                                   };

            ReportOptions options = new ReportOptions();
            List<int> selectedProjects = new List<int>();
            IssuesGridFilter tmp = new IssuesGridFilter();

            try
            {
                if (CurrentCard.IsNew || !CurrentCard.Options.ContainsKey(AppGuid))
                {
                    tmp = new IssuesGridFilter(HttpSessionManager.GetFilter(CurrentCard.Id, CurrentCard.Filter));

                    if (tmp == null)
                    {
                        tmp = CurrentCard.Options[AppGuid].FromJson<IssuesGridFilter>();
                    }

                    if (tmp.Projects == Constants.AllProjectsId.ToString())
                        selectedProjects.Add(Constants.AllProjectsId);
                    else
                        selectedProjects = tmp.GetProjects();
                }
                else
                {
                    options = CurrentCard.Options[AppGuid].FromJson<ReportOptions>();
                    
                    if (options.AllProjectsSelected)
                    {
                        selectedProjects.Add(Constants.AllProjectsId);
                    }
                    else if (options.ProjectIds.Count > 0)
                    {
                        selectedProjects.AddRange(options.ProjectIds);
                    }
                }
            }
            catch (Exception ex)
            {
                tmp = new IssuesGridFilter(HttpSessionManager.GetFilter(CurrentCard.Id, IssuesFilter.CreateProjectFilter(UserContext.User.Entity.Id, UserContext.Project.Entity.Id)));

                selectedProjects = tmp.GetProjects();
            }

            var viewableProjects = ProjectManager.GetAppViewableProjects(this);

            model.ReportOptions = options;
            //model.FieldList = GetFilterList(null); //TODO CHECK IF THIS CAN BE DELETED
            model.FilterList = GetFilterList(null);
            model.TimeList = GetTimeFilter(options.TimeTypeIds, options.ProjectIds);
            
            model.ProjectList = GetProjectFilter(selectedProjects);

            if (options.Reports == (int)ReportsModel.ReportTypes.TimeReportUser)
                options.ResourceIds = new List<int>() { CurrentUser.Entity.Id };


            int projectId = options.ProjectIds.Count > 0 ? options.ProjectIds[0] : 0;
            var reportsManager = new ReportManager(Cache, UserContext, GeminiContext);
            if (projectId == 0)
            {
                model.ResourceList = reportsManager.GetReourceFilter(options.ResourceIds, model.ProjectList.Select(p => p.Value.ToInt()).ToList()); //GetResourceList(options.ResourceId, model.ProjectList.Select(p => p.Value.ToInt()));
            }
            else
            {
                model.ResourceList = reportsManager.GetReourceFilter(options.ResourceIds, options.ProjectIds);
            }

            model.GroupByList = GetGroupByList(options.GroupBy);

            WidgetResult result = new WidgetResult();

            result.Markup = new WidgetMarkup("views\\Timesheet.cshtml", model);

            result.Success = true;

            return result;
        }

        [AppUrl("get")]
        public ActionResult Get(ReportOptions form)
        {
            if (form.ProjectIds.Contains(Constants.AllProjectsId))
            {
                var viewableProjects = ProjectManager.GetAppViewableProjects(this);

                form.ProjectIds = viewableProjects.Count > 0 ? viewableProjects.Select(p => p.Entity.Id).ToList() : new List<int>();
                form.AllProjectsSelected = true;
                //if (form.ProjectId == null || form.ProjectId.Length == 0) return GoAway();
            }

            ReportManager report = new ReportManager(Cache, UserContext, GeminiContext);
            _timeTypes = report.GetTimeFilter(form.TimeTypeIds, form.ProjectIds);
            _resources = report.GetReourceFilter(form.ResourceIds, form.ProjectIds);

            //if (!CanSeeProjectArea(AreaVisibility.Reports)) return GoAway();

            form.StartDate = ParseDateString.GetDateForString(form.StartDateString);
            form.EndDate = ParseDateString.GetDateForString(form.EndDateString);

            if (form.StartDate.HasValue) form.StartDate = form.StartDate.Value.ClearTime();
            if (form.EndDate.HasValue) form.EndDate = form.EndDate.Value.ClearTime();

            if (form.StartDate.HasValue && form.EndDate.HasValue && form.EndDate < form.StartDate)
                form.StartDate = form.EndDate.Value.AddDays(-14);


            switch ((ReportsModel.ReportTypes)form.Reports)
            {              
                case ReportsModel.ReportTypes.TimeReport:
                case ReportsModel.ReportTypes.TimeReportProject:
                case ReportsModel.ReportTypes.TimeReportUser:
                    form.StartDate = form.StartDate.HasValue ? form.StartDate.Value : DateTime.Today.FirstDayOfWeek().ClearTime();
                    form.EndDate = form.EndDate.HasValue ? form.EndDate.Value : form.StartDate.Value.AddDays(6).ClearTime();

                    return GetTimeReport(form);
                default:
                    return Content("Not Found");

            }
        }

        private ActionResult GetTimeReport(ReportOptions options)
        {
            var reportManager = new ReportManager(GeminiApp.Cache(), UserContext, GeminiContext);

            IEnumerable<TimeReportModel> timeReport = new Collection<TimeReportModel>();
            switch (options.Reports)
            {
                case (int)ReportsModel.ReportTypes.TimeReportProject:

                    timeReport = reportManager.GetTimeReport(options);
                    break;
                case (int)ReportsModel.ReportTypes.TimeReportUser:
                    options.ResourceIds = new List<int>() { CurrentUser.Entity.Id };
                    timeReport = reportManager.GetTimeReport(options);
                    break;
            }

            var resultModel = new ReportResultModel { StartDate = options.StartDate, EndDate = options.EndDate, StartDateString = options.StartDateString, EndDateString = options.EndDateString, Results = timeReport };

            if (options.GroupBy == 1)
            {
                return Json(JsonResponse(options, RenderPartialViewToString(this, AppManager.Instance.GetAppUrl(AppGuid, "views/_TimeResourceReport.cshtml"), resultModel)));
            }
            if (options.GroupBy == 2)
            {               
                return Json(JsonResponse(options,  RenderPartialViewToString(this, AppManager.Instance.GetAppUrl(AppGuid, "views/_TimeIssueReport.cshtml"), resultModel)));
            }
            resultModel.Dictionary = new Dictionary<int, string>();

            //TODO get time types for all projects involved.
            foreach (var timeType in new MetaManager(reportManager).TimeTypeGetAll())
            {
                resultModel.Dictionary.Add(timeType.Id, timeType.Label);
            }
            resultModel.Dictionary.Add(0, GetResource(ResourceKeys.Unassigned));

            return Json(JsonResponse(options, RenderPartialViewToString(this, AppManager.Instance.GetAppUrl(AppGuid, "views/_TimeTypeReport.cshtml"), resultModel)));
        }

        private JsonResponse JsonResponse(ReportOptions options, string html)
        {
            CurrentCard.Options[AppGuid] = options.ToJson();

            if (!CurrentCard.Url.HasValue())
            {
                CurrentCard.Url = NavigationHelper.GetReportsPageUrl(CurrentProject);
            }

            var r = new JsonResponse()
            {
                Success = true,
                Result = new { Html = html, SavedCard = CurrentCard, TimeTypes = _timeTypes, Resources = _resources }
            };
            return r;
        }

        private IEnumerable<SelectListItem> GetGroupByList(int? selected)
        {
            var dateGroup = new SelectListItem { Text = GetResource(ResourceKeys.Resource), Value = "1", Selected = selected.GetValueOrDefault() == 1 };
            var typeGroup = new SelectListItem { Text = GetResource(ResourceKeys.TimeType), Value = "3", Selected = selected.GetValueOrDefault() == 3 };
            var itemsGroup = new SelectListItem { Text = GetResource(ResourceKeys.Items), Value = "2", Selected = selected.GetValueOrDefault() == 2 };

            return new SelectList(new[] { dateGroup, typeGroup, itemsGroup }, "Value", "Text", selected);
        }

        private MultiSelectList GetProjectFilter(List<int> selectedProject)
        {
            var all = ProjectManager.GetActive(); 

            var viewableProjects = ProjectManager.GetAppViewableProjects(this);

            all.RemoveAll(p => !viewableProjects.Any(s => s.Entity.Id == p.Entity.Id));

            Project allProjects = new Project() { Id = Constants.AllProjectsId, Name = GetResource(ResourceKeys.AllProjects) };

            var allProjectsList = all.Select(p => p.Entity).ToList();
            allProjectsList.Insert(0, allProjects);

            if (selectedProject.Count == 0)
            {
                 if (allProjectsList.Count > 1)
                     selectedProject.Add(allProjectsList[1].Id);
                 else
                    selectedProject.Add(allProjectsList[0].Id);
            }

            return new MultiSelectList(allProjectsList, "id", "Name", selectedProject);
        }


        private IEnumerable<SelectListItem> GetFilterList(int? selected)
        {
            var cardsManager = new NavigationCardsManager(GeminiApp.Cache(), UserContext, GeminiContext);
            List<NavigationCard> navigationCards = new List<NavigationCard>();
            navigationCards.Add(new NavigationCard
            {
                Title = "Default",
                Id = 0
            });

            navigationCards.AddRange(cardsManager.GetUser(CurrentUser.Entity.Id).FindAll(f => f.CardType == ProjectTemplatePageType.Items));

            /*navigationCards.Insert(0, cardsManager.);*/
            IEnumerable<SelectListItem> result = new SelectList(navigationCards, "id", "ShortDisplay", selected);
            return result;
        }

        public MultiSelectList GetTimeFilter(List<string> selected, List<int> projectIds)
        {
            var manager = new ReportManager(Cache, UserContext, GeminiContext);
            return manager.GetTimeFilter(selected, projectIds);
        }
    }
    
}
