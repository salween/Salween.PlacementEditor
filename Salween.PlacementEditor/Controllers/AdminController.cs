using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Orchard.ContentManagement;
using Orchard.Themes;
using Orchard.ContentTypes;
using Orchard;
using Orchard.ContentManagement.MetaData;
using Orchard.Localization;
using Salween.PlacementEditor.Services;

namespace Salween.PlacementEditor.Controllers
{
    public class AdminController : Controller
    {
        public Localizer T;
        private IContentManager OrchardContentService;
        private IOrchardServices OrchardServices;
        private IContentDefinitionManager ContentTypeManager;
        private IPlacementServiceWrapper PlacementService;

        public AdminController(IContentManager contentService, IOrchardServices orchardService, IContentDefinitionManager contentTypeManager, IPlacementServiceWrapper placementService)
        {
            OrchardContentService = contentService;
            OrchardServices = orchardService;
            ContentTypeManager = contentTypeManager;
            PlacementService = placementService;
            T = NullLocalizer.Instance;
        }

        [Themed]
        public ActionResult EditPlacement(string contentType)
        {
            if (!OrchardServices.Authorizer.Authorize(Orchard.ContentTypes.Permissions.EditContentTypes, T("Not allowed to edit a content type.")))
                return new HttpUnauthorizedResult();

            var contentTypeDefinition = ContentTypeManager.GetTypeDefinition(contentType);

            if (contentTypeDefinition == null)
                return HttpNotFound();

            var placementModel = PlacementService.GetEditPlacementViewModel(contentTypeDefinition);

            return View(placementModel);
        }
    }
}