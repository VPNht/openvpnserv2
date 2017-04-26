using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grapevine.Interfaces.Server;
using Grapevine.Server.Attributes;
using Grapevine.Shared;
using Grapevine.Server;
using OpenVpn;

namespace OpenVpnService
{
    [RestResource]
    public class VPNResource
    {
        [RestRoute(HttpMethod = HttpMethod.GET, PathInfo = "/status")]
        public IHttpContext Status(IHttpContext context)
        {
            context.Response.StatusCode = HttpStatusCode.Ok;
            context.Response.SendResponse(ManagementClient.Instance.State.ToString());
            return context;
        }

        [RestRoute(HttpMethod = HttpMethod.GET, PathInfo = "/connect")]
        public IHttpContext Connect(IHttpContext context)
        {
            ManagementClient.Instance.Connect(53813);

            context.Response.StatusCode = HttpStatusCode.Ok;
            context.Response.SendResponse("");
            return context;
        }

        [RestRoute(HttpMethod = HttpMethod.GET, PathInfo = "/disconnect")]
        public IHttpContext Disconnect(IHttpContext context)
        {
            ManagementClient.Instance.Disconnect();

            context.Response.StatusCode = HttpStatusCode.Ok;
            context.Response.SendResponse("");
            return context;
        }
    }
}
