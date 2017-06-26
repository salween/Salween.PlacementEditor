using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Orchard.Mvc.Routes;

namespace Salween.PlacementEditor
{
    public class Routes : IRouteProvider
    {
        public IEnumerable<RouteDescriptor> GetRoutes()
        {
            return new[]
            {
                new RouteDescriptor
                {
                    Priority = 11,
                    Route = new Route(
                        "Admin/ContentTypes/EditPlacement/{contentType}",
                        new RouteValueDictionary
                        {
                            {"area", "Salween.PlacementEditor" },
                            {"controller", "Admin"},
                            {"action", "EditPlacement"},
                            {"contentType", UrlParameter.Optional}
                        },
                        new RouteValueDictionary(),
                        new RouteValueDictionary{
                            {"area", "Salween.PlacementEditor" }
                        },
                        new MvcRouteHandler())
                }
            };
        }

        public void GetRoutes(ICollection<RouteDescriptor> routes)
        {
            foreach (RouteDescriptor routeDescriptor in GetRoutes())
                routes.Add(routeDescriptor);
        }
    }
}