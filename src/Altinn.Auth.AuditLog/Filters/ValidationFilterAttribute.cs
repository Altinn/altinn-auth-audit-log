using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;

namespace Altinn.Auth.AuditLog.Filters
{
    [ExcludeFromCodeCoverage]
    public class ValidationFilterAttribute : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                context.Result = new UnprocessableEntityObjectResult(context.ModelState);
            }
        }

        /// <summary>
        /// Post execution
        /// </summary>
        /// <param name="context">context</param>        
        public void OnActionExecuted(ActionExecutedContext context) {}
    }
}
