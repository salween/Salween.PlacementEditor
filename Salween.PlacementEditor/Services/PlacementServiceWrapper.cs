using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Routing;
using System.Web.Script.Serialization;
using Orchard;
using Orchard.ContentManagement;
using Orchard.ContentManagement.Drivers;
using Orchard.ContentManagement.Handlers;
using Orchard.ContentManagement.MetaData.Models;
using Orchard.ContentTypes.Extensions;
using Orchard.ContentTypes.Services;
using Orchard.ContentTypes.ViewModels;
using Orchard.DisplayManagement;
using Orchard.DisplayManagement.Descriptors;
using Orchard.FileSystems.VirtualPath;
using Orchard.Logging;
using Orchard.Themes.Services;
using Orchard.UI;
using Orchard.UI.Zones;
using Salween.PlacementEditor.Models;

namespace Salween.PlacementEditor.Services
{
    public interface IPlacementServiceWrapper : IDependency
    {
        EditPlacementViewModel GetEditPlacementViewModel(ContentTypeDefinition contentTypeDefinition);
    }

    public class PlacementServiceWrapper : IPlacementServiceWrapper
    {
        private IPlacementService PlacementService;
        private ISiteThemeService SiteThemeService;
        private IShapeTableLocator ShapeTableLocator;
        private RequestContext RequestContext;
        private IVirtualPathProvider VirtualPathProvider;
        private IShapeFactory ShapeFactory;
        private IContentManager ContentManager;
        private IEnumerable<IContentPartDriver> ContentPartDrivers;
        private IEnumerable<IContentFieldDriver> ContentFieldDrivers;
        public ILogger Logger { get; set; }

        public PlacementServiceWrapper(IPlacementService placementService, ISiteThemeService siteThemeService, IShapeTableLocator shapeTableLocator,
            RequestContext requestContext, IVirtualPathProvider virtualPathProvider, IShapeFactory shapeFactory, IContentManager contentManager,
            IEnumerable<IContentPartDriver> contentPartDrivers, IEnumerable<IContentFieldDriver> contentFieldDrivers)
        {
            PlacementService = placementService;
            SiteThemeService = siteThemeService;
            ShapeTableLocator = shapeTableLocator;
            RequestContext = requestContext;
            VirtualPathProvider = virtualPathProvider;
            ShapeFactory = shapeFactory;
            ContentManager = contentManager;
            ContentPartDrivers = contentPartDrivers;
            ContentFieldDrivers = contentFieldDrivers;

            Logger = NullLogger.Instance;
        }

        public EditPlacementViewModel GetEditPlacementViewModel(ContentTypeDefinition contentTypeDefinition)
        {
            var storedPlacement = GetStoredTabForPlacement(contentTypeDefinition, PlacementType.Editor);
            var placementFromPlacementEditor = GetTabFromLocation(contentTypeDefinition.Name);

            return new EditPlacementViewModel
            {
                PlacementSettings = storedPlacement,
                AllPlacements = placementFromPlacementEditor.OrderBy(x => x.PlacementSettings.Position, new FlatPositionComparer()).ThenBy(x => x.PlacementSettings.ShapeType).ToList(),
                ContentTypeDefinition = contentTypeDefinition,
            };
        }

        private TabPlacementSettings[] GetStoredTabForPlacement(ContentTypeDefinition contentTypeDefinition, PlacementType placementType)
        {
            var currentSettings = contentTypeDefinition.Settings;
            var serializer = new JavaScriptSerializer();

            currentSettings.TryGetValue("ContentTypeSettings.Placement." + placementType, out string placement);

            return String.IsNullOrEmpty(placement) ? new TabPlacementSettings[0] : serializer.Deserialize<TabPlacementSettings[]>(placement);
        }

        private IEnumerable<DriverResultPlacement> GetTabFromLocation(string contentType)
        {
            var content = ContentManager.New(contentType);

            dynamic itemShape = CreateItemShape("Content_Edit");
            itemShape.ContentItem = content;

            var context = new BuildEditorContext(itemShape, content, String.Empty, ShapeFactory);
            BindPlacement(context, null, "Content");

            var placementSettings = new List<DriverResultPlacement>();

            ContentPartDrivers.Invoke(driver => {
                var result = driver.BuildEditor(context);
                if (result != null)
                {
                    placementSettings.AddRange(ExtractPlacement(result, context));
                }
            }, Logger);

            ContentFieldDrivers.Invoke(driver => {
                var result = driver.BuildEditorShape(context);
                if (result != null)
                {
                    placementSettings.AddRange(ExtractPlacement(result, context));
                }
            }, Logger);

            foreach (var placementSetting in placementSettings)
            {
                yield return placementSetting;
            }
        }
        private void BindPlacement(BuildShapeContext context, string displayType, string stereotype)
        {
            context.FindPlacement = (partShapeType, differentiator, defaultLocation) => {

                var theme = SiteThemeService.GetSiteTheme();
                var shapeTable = ShapeTableLocator.Lookup(theme.Id);

                var request = RequestContext.HttpContext.Request;

                ShapeDescriptor descriptor;
                if (shapeTable.Descriptors.TryGetValue(partShapeType, out descriptor))
                {
                    var placementContext = new ShapePlacementContext
                    {
                        Content = context.ContentItem,
                        ContentType = context.ContentItem.ContentType,
                        Stereotype = stereotype,
                        DisplayType = displayType,
                        Differentiator = differentiator,
                        Path = VirtualPathUtility.AppendTrailingSlash(VirtualPathProvider.ToAppRelative(request.Path)) // get the current app-relative path, i.e. ~/my-blog/foo
                    };

                    // define which location should be used if none placement is hit
                    descriptor.DefaultPlacement = defaultLocation;

                    var placement = descriptor.Placement(placementContext);
                    if (placement != null)
                    {
                        placement.Source = placementContext.Source;
                        return placement;
                    }
                }

                return new PlacementInfo
                {
                    Location = defaultLocation,
                    Source = String.Empty
                };
            };
        }

        private IEnumerable<DriverResultPlacement> ExtractPlacement(DriverResult result, BuildShapeContext context)
        {
            if (result is CombinedResult)
            {
                foreach (var subResult in ((CombinedResult)result).GetResults())
                {
                    foreach (var placement in ExtractPlacement(subResult, context))
                    {
                        yield return placement;
                    }
                }
            }
            else if (result is ContentShapeResult)
            {
                var contentShapeResult = (ContentShapeResult)result;

                var placement = context.FindPlacement(
                    contentShapeResult.GetShapeType(),
                    contentShapeResult.GetDifferentiator(),
                    contentShapeResult.GetLocation()
                    );

                string zone = placement.Location;
                string position = String.Empty;
                string tab = String.Empty;

                // if no placement is found, it's hidden, e.g., no placement was found for the specific ContentType/DisplayType
                if (placement.Location != null)
                {
                    var delimiterIndex = placement.Location.IndexOf(':');
                    var tabIndex = placement.Location.IndexOf('#');
                    if (delimiterIndex >= 0)
                    {
                        zone = placement.Location.Substring(0, delimiterIndex);
                        if (tabIndex >= 0)
                        {
                            position = placement.Location.Substring(delimiterIndex + 1, (tabIndex - delimiterIndex - 1));
                            tab = placement.Location.Substring(tabIndex + 1);
                        }
                        else
                        {
                            position = placement.Location.Substring(delimiterIndex + 1);
                        }
                    }
                }

                var content = ContentManager.New(context.ContentItem.ContentType);

                dynamic itemShape = CreateItemShape("Content_Edit");
                itemShape.ContentItem = content;

                if (context is BuildDisplayContext)
                {
                    var newContext = new BuildDisplayContext(itemShape, content, "Detail", "", context.New);
                    BindPlacement(newContext, "Detail", "Content");
                    contentShapeResult.Apply(newContext);
                }
                else
                {
                    var newContext = new BuildEditorContext(itemShape, content, "", context.New);
                    BindPlacement(newContext, null, "Content");
                    contentShapeResult.Apply(newContext);
                }


                yield return new DriverResultPlacement
                {
                    Shape = itemShape.Content,
                    ShapeResult = contentShapeResult,
                    PlacementSettings = new TabPlacementSettings
                    {
                        ShapeType = contentShapeResult.GetShapeType(),
                        Zone = zone,
                        Position = position,
                        Tab = tab,
                        Differentiator = contentShapeResult.GetDifferentiator() ?? String.Empty
                    }
                };
            }
        }

        private dynamic CreateItemShape(string actualShapeType)
        {
            var zoneHolding = new ZoneHolding(() => ShapeFactory.Create("ContentZone", Arguments.Empty()));
            zoneHolding.Metadata.Type = actualShapeType;
            return zoneHolding;
        }
    }
}