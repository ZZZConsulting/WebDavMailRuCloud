﻿using System.Collections.Generic;
using NWebDav.Server;
using NWebDav.Server.Http;
using YaR.Clouds.WebDavStore.CustomHandlers;

namespace YaR.Clouds.WebDavStore
{
    public class RequestHandlerFactory : IRequestHandlerFactory 
    {
        private static readonly IDictionary<string, IRequestHandler> RequestHandlers = new Dictionary<string, IRequestHandler>
        {   
            { "COPY",      new CopyHandler() },
            { "DELETE",    new DeleteHandler() },
            { "GET",       new GetAndHeadHandler() },
            { "HEAD",      new GetAndHeadHandler() },
            { "LOCK",      new NWebDav.Server.Handlers.LockHandler() },
            { "MKCOL",     new MkcolHandler() },
            { "MCOL",      new MkcolHandler() },
            { "MOVE",      new MoveHandler() },
            { "OPTIONS",   new NWebDav.Server.Handlers.OptionsHandler() },
            { "PROPFIND",  new NWebDav.Server.Handlers.PropFindHandler() },
            { "PROPPATCH", new NWebDav.Server.Handlers.PropPatchHandler() },
            { "PUT",       new NWebDav.Server.Handlers.PutHandler() },
            { "UNLOCK",    new NWebDav.Server.Handlers.UnlockHandler() }
        };

        public IRequestHandler GetRequestHandler(IHttpContext httpContext)
        {
            return !RequestHandlers.TryGetValue(httpContext.Request.HttpMethod, out var requestHandler) 
                ? null 
                : requestHandler;
        }

        public static IEnumerable<string> AllowedMethods => RequestHandlers.Keys;
    }
}
