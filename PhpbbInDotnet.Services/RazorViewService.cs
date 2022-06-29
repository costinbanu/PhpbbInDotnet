using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class RazorViewService : IRazorViewService
    {
        private readonly ICompositeViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IActionContextAccessor _actionContextAccessor;

        public RazorViewService(ICompositeViewEngine viewEngine, ITempDataProvider tempDataProvider, IActionContextAccessor actionContextAccessor)
        {
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
            _actionContextAccessor = actionContextAccessor;
        }

        public async Task<string> RenderRazorViewToString(string viewName, object model)
        {
            var httpContext = _actionContextAccessor.ActionContext?.HttpContext ?? throw new Exception($"Error rendering view '{viewName}': Expected a ActionContext but found none");
            var pageContext = new PageContext(_actionContextAccessor.ActionContext)
            {
                ActionDescriptor = (CompiledPageActionDescriptor)_actionContextAccessor.ActionContext.ActionDescriptor
            };
            var actionContext = new ActionContext(httpContext, httpContext.GetRouteData(), pageContext.ActionDescriptor);
            var viewResult = _viewEngine.FindView(actionContext, viewName, false);

            var viewDictionary = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = model
            };

            using var sw = new StringWriter();
            var viewContext = new ViewContext(
                actionContext,
                viewResult.View ?? throw new Exception($"{viewName} does not match any available view"),
                viewDictionary,
                new TempDataDictionary(actionContext.HttpContext, _tempDataProvider),
                sw,
                new HtmlHelperOptions()
            )
            {
                RouteData = httpContext.GetRouteData()
            };

            await viewResult.View.RenderAsync(viewContext);
            return sw.GetStringBuilder().ToString();
        }
    }
}
