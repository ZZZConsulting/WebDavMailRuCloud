﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NWebDav.Server;
using NWebDav.Server.Helpers;
using NWebDav.Server.Http;
using NWebDav.Server.Stores;

namespace YaR.Clouds.WebDavStore.CustomHandlers
{
    public class MoveHandler : IRequestHandler
    {
        /// <summary>
        /// Handle a MOVE request.
        /// </summary>
        /// <param name="httpContext">
        /// The HTTP context of the request.
        /// </param>
        /// <param name="store">
        /// Store that is used to access the collections and items.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous MOVE operation. The task
        /// will always return <see langword="true"/> upon completion.
        /// </returns>
        public async Task<bool> HandleRequestAsync(IHttpContext httpContext, IStore store)
        {
            // Obtain request and response
            var request = httpContext.Request;
            var response = httpContext.Response;

            // We should always move the item from a parent container
            var splitSourceUri = RequestHelper.SplitUri(request.Url);

            // Obtain the destination
            var destinationUri = GetDestinationUri(request);
            if (destinationUri == null)
            {
                // Bad request
                response.SetStatus(DavStatusCode.BadRequest, "Destination header is missing.");
                return true;
            }

            // Make sure the source and destination are different
            if (request.Url.AbsoluteUri.Equals(destinationUri.AbsoluteUri, StringComparison.CurrentCultureIgnoreCase))
            {
                // Forbidden
                response.SetStatus(DavStatusCode.Forbidden, "Source and destination cannot be the same.");
                return true;
            }

            // We should always move the item to a parent
            var splitDestinationUri = RequestHelper.SplitUri(destinationUri);

            // Obtain source collection
            var sourceCollection = await store.GetCollectionAsync(splitSourceUri.CollectionUri, httpContext).ConfigureAwait(false);
            if (sourceCollection == null)
            {
                // Source not found
                response.SetStatus(DavStatusCode.NotFound);
                return true;
            }

            // Obtain destination collection
            var destinationCollection =
                splitDestinationUri.CollectionUri.AbsoluteUri.Equals(
                    splitSourceUri.CollectionUri.AbsoluteUri, StringComparison.InvariantCultureIgnoreCase)
                // If source and destination collections are the same, there is no need to request a server twice
                ? sourceCollection
                : await store.GetCollectionAsync(splitDestinationUri.CollectionUri, httpContext).ConfigureAwait(false);
            if (destinationCollection == null)
            {
                // Source not found
                response.SetStatus(DavStatusCode.NotFound);
                return true;
            }

            // Check if the Overwrite header is set
            var overwrite = request.GetOverwrite();

            // Keep track of all errors
            var errors = new UriResultCollection();

            // Move collection
            await MoveAsync(sourceCollection, splitSourceUri.Name, destinationCollection,
                splitDestinationUri.Name, overwrite, httpContext, splitDestinationUri.CollectionUri, errors).ConfigureAwait(false);

            // Check if there are any errors
            if (errors.HasItems)
            {
                // Obtain the status document
                var xDocument = new XDocument(errors.GetXmlMultiStatus());

                // Stream the document
                await response.SendResponseAsync(DavStatusCode.MultiStatus, xDocument).ConfigureAwait(false);
            }
            else
            {
                // Set the response
                response.SetStatus(DavStatusCode.Ok);
            }

            return true;
        }

        private static WebDavUri GetDestinationUri(IHttpRequest request)
        {
            // Obtain the destination
            string destinationHeader = request.GetHeaderValue("Destination");
            
            if (destinationHeader == null)
                return null;

            // Create the destination URI
            var uri = destinationHeader.StartsWith("/")
                ? new WebDavUri(request.Url.BaseUrl, destinationHeader)
                : new WebDavUri(destinationHeader);

            return uri;
        }



        private static async Task MoveAsync(IStoreCollection sourceCollection, string sourceName, IStoreCollection destinationCollection, string destinationName, bool overwrite, IHttpContext httpContext, WebDavUri baseUri, UriResultCollection errors)
        {
            // Determine the new base URI
            var subBaseUri = UriHelper.Combine(baseUri, destinationName);

            // Items should be moved directly
            var result = await sourceCollection.MoveItemAsync(sourceName, destinationCollection, destinationName, overwrite, httpContext).ConfigureAwait(false);
            if (result.Result != DavStatusCode.Created && result.Result != DavStatusCode.NoContent)
                errors.AddResult(subBaseUri, result.Result);
        }
    }

    internal class UriResultCollection
    {
        private readonly struct UriResult
        {
            private WebDavUri Uri { get; }
            private DavStatusCode Result { get; }

            public UriResult(WebDavUri uri, DavStatusCode result)
            {
                Uri = uri;
                Result = result;
            }

            public XElement GetXmlResponse()
            {
                var href = Uri.AbsoluteUri;
                var statusText = $"HTTP/1.1 {(int)Result} {Result.GetStatusDescription()}";
                return new XElement(WebDavNamespaces.DavNsResponse,
                    new XElement(WebDavNamespaces.DavNsHref, href),
                    new XElement(WebDavNamespaces.DavNsStatus, statusText));
            }
        }

        private readonly IList<UriResult> _results = new List<UriResult>();

        public bool HasItems => _results.Any();

        public void AddResult(WebDavUri uri, DavStatusCode result)
        {
            _results.Add(new UriResult(uri, result));
        }

        public XElement GetXmlMultiStatus()
        {
            var xMultiStatus = new XElement(WebDavNamespaces.DavNsMultiStatus);
            foreach (var result in _results)
                xMultiStatus.Add(result.GetXmlResponse());
            return xMultiStatus;
        }
    }
}
