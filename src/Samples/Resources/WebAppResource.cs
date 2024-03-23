using System.Threading.Tasks;
using Grapevine;
using WebApp.Api;

namespace Samples.Resources;

public class WebAppResource : WebAppApi
{
    public override async Task Close(IHttpContext context)
    {
        // TODO: Implement your operation here.
        context.Response.StatusCode = HttpStatusCode.NoContent;
        await context.Response.SendResponseAsync();
    }

    public override async Task OpenUrl(IHttpContext context)
    {
        // TODO: Implement your operation here.
        context.Response.StatusCode = HttpStatusCode.Ok;
        await context.Response.SendResponseAsync();
    }
}