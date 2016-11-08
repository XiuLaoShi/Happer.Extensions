﻿using System;
using System.Collections.Generic;
using Happer.Buffer;
using Happer.Http;
using Happer.Http.Responses;
using Happer.Http.Routing;
using Happer.Http.Routing.Trie;
using Happer.Http.Serialization;
using Happer.WebSockets;
using Happer.Serialization;
using Happer.StaticContent;

namespace Happer.TestHttpServer
{
    public class HybridBootstrapper
    {
        public HybridBootstrapper()
        {
        }

        public HybridEngine BootWith(IHybridModuleContainer container)
        {
            if (container == null)
                throw new ArgumentNullException("container");

            var staticContentProvider = BuildStaticContentProvider();
            var requestDispatcher = BuildRequestDispatcher(container);
            var webSocketDispatcher = BuildWebSocketDispatcher(container);

            return new HybridEngine(staticContentProvider, requestDispatcher, webSocketDispatcher);
        }

        private StaticContentProvider BuildStaticContentProvider()
        {
            var rootPathProvider = new RootPathProvider();
            var staticContnetConventions = new StaticContentsConventions(new List<Func<Context, string, Response>>
            {
                StaticContentConventionBuilder.AddDirectory("Content")
            });
            var staticContentProvider = new StaticContentProvider(rootPathProvider, staticContnetConventions);

            FileResponse.SafePaths.Add(rootPathProvider.GetRootPath());

            return staticContentProvider;
        }

        private RequestDispatcher BuildRequestDispatcher(IHybridModuleContainer container)
        {
            var moduleCatalog = new ModuleCatalog(
                    () => { return container.GetAllModules(); },
                    (Type moduleType) => { return container.GetModule(moduleType); }
                );

            var routeSegmentExtractor = new RouteSegmentExtractor();
            var routeDescriptionProvider = new RouteDescriptionProvider();
            var routeCache = new RouteCache(routeSegmentExtractor, routeDescriptionProvider);
            routeCache.BuildCache(moduleCatalog.GetAllModules());

            var trieNodeFactory = new TrieNodeFactory();
            var routeTrie = new RouteResolverTrie(trieNodeFactory);
            routeTrie.BuildTrie(routeCache);

            var serializers = new List<ISerializer>() { new JsonSerializer(), new XmlSerializer() };
            var responseFormatterFactory = new ResponseFormatterFactory(serializers);
            var moduleBuilder = new ModuleBuilder(responseFormatterFactory);

            var routeResolver = new RouteResolver(moduleCatalog, moduleBuilder, routeTrie);

            var negotiator = new ResponseNegotiator();
            var routeInvoker = new RouteInvoker(negotiator);
            var requestDispatcher = new RequestDispatcher(routeResolver, routeInvoker);

            return requestDispatcher;
        }

        private WebSocketDispatcher BuildWebSocketDispatcher(IHybridModuleContainer container)
        {
            var moduleCatalog = new WebSocketModuleCatalog(
                    () => { return container.GetAllWebSocketModules(); },
                    (Type moduleType) => { return container.GetWebSocketModule(moduleType); }
                );

            var routeResolver = new WebSocketRouteResolver(moduleCatalog);
            var bufferManager = new SegmentBufferManager(1024, 8192, 1, true);

            return new WebSocketDispatcher(routeResolver, bufferManager);
        }
    }
}
