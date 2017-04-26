using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grapevine.Interfaces.Server;
using Grapevine.Server.Attributes;
using Grapevine.Shared;

namespace OpenVpnService
{
    [RestResource]
    public class VPNResource
    {
        [RestRoute(HttpMethod = HttpMethod.GET, PathInfo = "/status")]
        public IHttpContext Status(IHttpContext context)
        {
            return context;
        }

        [RestRoute(HttpMethod = HttpMethod.GET, PathInfo = "/connect")]
        public IHttpContext Connect(IHttpContext context)
        {
            return context;
        }

        [RestRoute(HttpMethod = HttpMethod.GET, PathInfo = "/disconnect")]
        public IHttpContext Disconnect(IHttpContext context)
        {
            return context;
        }
    }
}
