﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.OData.Common;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.Mvc;
using Microsoft.OData.Core;
using Microsoft.OData.Core.UriParser;
using Microsoft.OData.Edm;
using Microsoft.AspNet.Mvc.Infrastructure;
using System.Globalization;

namespace Microsoft.AspNet.OData.Query
{
    // TODO: Replace with full version in the future.
    public class ODataQueryOptions
    {
        private readonly IAssemblyProvider _assemblyProvider;
        private readonly ODataQueryOptionParser _queryOptionParser;

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataQueryOptions"/> class based on the incoming request and some metadata information from
        /// the <see cref="ODataQueryContext"/>.
        /// </summary>
        /// <param name="context">The <see cref="ODataQueryContext"/> which contains the <see cref="IEdmModel"/> and some type information.</param>
        /// <param name="request">The incoming request message.</param>
        public ODataQueryOptions(ODataQueryContext context, HttpRequest request)
        {
            if (context == null)
            {
                throw Error.ArgumentNull("context");
            }

            if (request == null)
            {
                throw Error.ArgumentNull("request");
            }

            _assemblyProvider = request.AssemblyProvider();

            Context = context;
            Request = request;
            RawValues = new ODataRawQueryOptions();

            var queryOptionDict = request.Query.ToDictionary(p => p.Key, p => p.Value.FirstOrDefault());
            _queryOptionParser = new ODataQueryOptionParser(
                context.Model,
                context.ElementType,
                context.NavigationSource,
                queryOptionDict);

            BuildQueryOptions(queryOptionDict);
        }

        /// <summary>
        ///  Gets the given <see cref="ODataQueryContext"/>
        /// </summary>
        public ODataQueryContext Context { get; }

        /// <summary>
        /// Gets the request message associated with this instance.
        /// </summary>
        public HttpRequest Request { get; }

        /// <summary>
        /// Gets the raw string of all the OData query options
        /// </summary>
        public ODataRawQueryOptions RawValues { get; }

        /// <summary>
        /// Gets the <see cref="FilterQueryOption"/>.
        /// </summary>
        public FilterQueryOption Filter { get; private set; }

        public SelectExpandQueryOption SelectExpand { get; private set; }

        /// <summary>
        /// Specifies a non-negative integer n that excludes the first n items of the queried collection from the result.         
        /// </summary>
        /// <remarks>
        /// Corresponds to the $skip query option.  The service returns items starting at position n+1.
        /// </remarks>
        public int? Skip { get; private set; }

        /// <summary>
        /// Specifies a non-negative integer n that limits the number of items returned from a collection.
        /// </summary>
        /// <remarks>
        /// Corresponds to the $take query option.  he service returns the number of available items up to but not greater than the specified value n.
        /// </remarks>
        public int? Top { get; private set; }

        /// <summary>
        /// Apply the individual query to the given IQueryable in the right order.
        /// </summary>
        /// <param name="query">The original <see cref="IQueryable"/>.</param>
        /// <param name="querySettings">The settings to use in query composition.</param>
        /// <returns>The new <see cref="IQueryable"/> after the query has been applied to.</returns>
        public virtual IQueryable ApplyTo(IQueryable query, ODataQuerySettings querySettings)
        {
            if (query == null)
            {
                throw Error.ArgumentNull("query");
            }

            // Construct the actual query and apply them in the following order: filter
            if (Filter != null)
            {
                query = Filter.ApplyTo(query, querySettings, _assemblyProvider);
            }

            if (SelectExpand != null)
            {
                query = SelectExpand.ApplyTo(query, querySettings, _assemblyProvider);
            }

            if (Skip.HasValue)
            {
                query = ExpressionHelpers.Skip(query, Skip.Value, Context.ElementClrType, false);
            }

            int? take = null;
            if (querySettings.PageSize.HasValue)
            {
                take = Math.Min(querySettings.PageSize.Value, int.MaxValue);
            }
            if (Top.HasValue)
            {
                take = Math.Min(Top.Value, take ?? int.MaxValue);
            }
            if (take.HasValue)
            {
                query = ExpressionHelpers.Take(query, take.Value, Context.ElementClrType, false);
            }

            return query;
        }

        private void BuildQueryOptions(IDictionary<string, string> queryParameters)
        {
            foreach (var kvp in queryParameters)
            {
                switch (kvp.Key.ToLowerInvariant())
                {
                    case "$filter":
                        ThrowIfEmpty(kvp.Value, "$filter");
                        RawValues.Filter = kvp.Value;
                        Filter = new FilterQueryOption(kvp.Value, Context, _queryOptionParser);
                        break;
                    case "$orderby":
                        ThrowIfEmpty(kvp.Value, "$orderby");
                        RawValues.OrderBy = kvp.Value;
                        break;
                    case "$top":
                        ThrowIfEmpty(kvp.Value, "$top");
                        RawValues.Top = kvp.Value;
                        Top = TryParseNonNegativeInteger("$top", kvp.Value);
                        break;
                    case "$skip":
                        ThrowIfEmpty(kvp.Value, "$skip");
                        RawValues.Skip = kvp.Value;
                        Skip = TryParseNonNegativeInteger("$skip", kvp.Value);
                        break;
                    case "$select":
                        RawValues.Select = kvp.Value;
                        break;
                    case "$count":
                        ThrowIfEmpty(kvp.Value, "$count");
                        RawValues.Count = kvp.Value;
                        break;
                    case "$expand":
                        RawValues.Expand = kvp.Value;
                        // TODO Parse the select statement if any
                        Request.ODataProperties().SelectExpandClause = _queryOptionParser.ParseSelectAndExpand();
                        SelectExpand = new SelectExpandQueryOption(string.Empty, kvp.Value, Context, _queryOptionParser, Request);
                        break;
                    case "$format":
                        RawValues.Format = kvp.Value;
                        break;
                    case "$skiptoken":
                        RawValues.SkipToken = kvp.Value;
                        break;
                }
            }
        }

        private static void ThrowIfEmpty(string queryValue, string queryName)
        {
            if (String.IsNullOrWhiteSpace(queryValue))
            {
                throw new ODataException(Error.Format("Query '{0}' cannot be empty", queryName));
            }
        }

        private static int TryParseNonNegativeInteger(string parameterName,
            string value,
            System.Globalization.NumberStyles styles = System.Globalization.NumberStyles.Any,
            IFormatProvider provider = null)
        {
            provider = provider ?? CultureInfo.InvariantCulture;
            int n;
            if (int.TryParse(value, styles, provider, out n) == false)
            {
                throw new ODataException($"Query '{parameterName}' must be an integer.");
            }
            if (n < 0)
            {
                throw new ODataException($"Query '{parameterName}' must be an non-negative integer.");
            }
            return n;
        }
    }
}