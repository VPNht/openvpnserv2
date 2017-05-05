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
            ManagementClient client = ManagementClient.Instance;

            StringBuilder response = new StringBuilder();

            response.Append("{");

            if (ManagementClient.Instance.OpenVpnState == OpenVpnState.CONNECTED)
            {
                response.AppendFormat("\"clientState\": \"{0}\",", client.ClientState.ToString());
                response.AppendFormat("\"connectionState\": \"{0}\",", client.OpenVpnState.ToString());
                response.AppendFormat("\"localIP\": \"{0}\",", client.LocalIP.ToString());
                response.AppendFormat("\"remoteIP\": \"{0}\",", client.RemoteIP.ToString());
                response.AppendFormat("\"uploadedBytes\": {0},", client.UploadedBytes);
                response.AppendFormat("\"downloadedBytes\": {0}", client.DownloadedBytes);
            }
            else
            {
                response.AppendFormat("\"clientState\": \"{0}\",", client.ClientState.ToString());
                response.AppendFormat("\"connectionState\": \"{0}\"", client.OpenVpnState.ToString());
            }

            response.Append("}");

            context.Response.StatusCode = HttpStatusCode.Ok;
            context.Response.SendResponse(response.ToString());
            return context;
        }

        [RestRoute(HttpMethod = HttpMethod.GET, PathInfo = "/connect")]
        public IHttpContext Connect(IHttpContext context)
        {
            var encodedUsername = context.Request.QueryString["u"] ?? "";
            var encodedPassword = context.Request.QueryString["p"] ?? "";

            ManagementClient.Instance.Username = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(encodedUsername));
            ManagementClient.Instance.Password = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(encodedPassword));
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
