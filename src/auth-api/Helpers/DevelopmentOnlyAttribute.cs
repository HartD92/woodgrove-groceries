using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace woodgroveapi.Helpers;

/// <summary>
/// Restricts a controller or action to the Development environment only.
/// Returns 404 in all other environments so debug endpoints are never exposed in Production.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class DevelopmentOnlyAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var env = context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        if (!env.IsDevelopment())
        {
            context.Result = new NotFoundResult();
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
