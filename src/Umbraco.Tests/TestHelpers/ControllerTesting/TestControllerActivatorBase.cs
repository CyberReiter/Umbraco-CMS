using System;
using System.Collections;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Dispatcher;
using System.Web.Security;
using Moq;
using Semver;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.Dictionary;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Security;
using Umbraco.Core.Services;
using Umbraco.Tests.TestHelpers.Stubs;
using Umbraco.Web;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.Routing;
using Umbraco.Web.Security;
using Umbraco.Web.WebApi;

namespace Umbraco.Tests.TestHelpers.ControllerTesting
{
    /// <summary>
    /// Used to mock all of the services required for re-mocking and testing controllers
    /// </summary>
    /// <remarks>
    /// A more complete version of this is found in the Umbraco REST API project but this has the basics covered
    /// </remarks>
    public abstract class TestControllerActivatorBase : DefaultHttpControllerActivator, IHttpControllerActivator
    {
        IHttpController IHttpControllerActivator.Create(HttpRequestMessage request, HttpControllerDescriptor controllerDescriptor, Type controllerType)
        {
            if (typeof(UmbracoApiControllerBase).IsAssignableFrom(controllerType))
            {
                var owinContext = request.TryGetOwinContext().Result;
                
                var mockedUserService = Mock.Of<IUserService>();

                var mockedMigrationService = new Mock<IMigrationEntryService>();
                //set it up to return anything so that the app ctx is 'Configured'
                mockedMigrationService.Setup(x => x.FindEntry(It.IsAny<string>(), It.IsAny<SemVersion>())).Returns(Mock.Of<IMigrationEntry>());

                var serviceContext = new ServiceContext(
                    userService: mockedUserService,
                    migrationEntryService: mockedMigrationService.Object,
                    localizedTextService:Mock.Of<ILocalizedTextService>(),
                    sectionService:Mock.Of<ISectionService>());

                //ensure the configuration matches the current version for tests
                SettingsForTests.ConfigurationStatus = Current.RuntimeState.SemanticVersion.ToSemanticString();

                // fixme v8?
                ////new app context
                //var dbCtx = new Mock<DatabaseContext>(Mock.Of<IDatabaseFactory>(), Mock.Of<ILogger>(), Mock.Of<ISqlSyntaxProvider>(), "test");
                ////ensure these are set so that the appctx is 'Configured'
                //dbCtx.Setup(x => x.CanConnect).Returns(true);
                //dbCtx.Setup(x => x.IsDatabaseConfigured).Returns(true);
                //var appCtx = ApplicationContext.EnsureContext(
                //    dbCtx.Object,
                //    //pass in mocked services
                //    serviceContext,
                //    CacheHelper.CreateDisabledCacheHelper(),
                //    new ProfilingLogger(Mock.Of<ILogger>(), Mock.Of<IProfiler>()),
                //    true);

                //httpcontext with an auth'd user
                var httpContext = Mock.Of<HttpContextBase>(
                    http => http.User == owinContext.Authentication.User
                            //ensure the request exists with a cookies collection    
                            && http.Request == Mock.Of<HttpRequestBase>(r => r.Cookies == new HttpCookieCollection())
                            //ensure the request exists with an items collection    
                            && http.Items == Mock.Of<IDictionary>());
                //chuck it into the props since this is what MS does when hosted and it's needed there
                request.Properties["MS_HttpContext"] = httpContext;                

                var backofficeIdentity = (UmbracoBackOfficeIdentity) owinContext.Authentication.User.Identity;

                var webSecurity = new Mock<WebSecurity>(null, null);

                //mock CurrentUser
                webSecurity.Setup(x => x.CurrentUser)
                    .Returns(Mock.Of<IUser>(u => u.IsApproved == true
                                                 && u.IsLockedOut == false
                                                 && u.AllowedSections == backofficeIdentity.AllowedApplications
                                                 && u.Email == "admin@admin.com"
                                                 && u.Id == (int) backofficeIdentity.Id
                                                 && u.Language == "en"
                                                 && u.Name == backofficeIdentity.RealName
                                                 && u.StartContentIds == backofficeIdentity.StartContentNodes
                                                 && u.StartMediaIds == backofficeIdentity.StartMediaNodes
                                                 && u.Username == backofficeIdentity.Username));

                //mock Validate
                webSecurity.Setup(x => x.ValidateCurrentUser())
                    .Returns(() => true);               
                webSecurity.Setup(x => x.UserHasAppAccess(It.IsAny<string>(), It.IsAny<IUser>()))
                    .Returns(() => true);

                var umbCtx = UmbracoContext.EnsureContext(
                    //set the user of the HttpContext
                    new TestUmbracoContextAccessor(),
                    httpContext,
                    Mock.Of<IFacadeService>(),
                    webSecurity.Object,
                    Mock.Of<IUmbracoSettingsSection>(section => section.WebRouting == Mock.Of<IWebRoutingSection>(routingSection => routingSection.UrlProviderMode == UrlProviderMode.Auto.ToString())),
                    Enumerable.Empty<IUrlProvider>(),
                    true); //replace it

                var urlHelper = new Mock<IUrlProvider>();
                urlHelper.Setup(provider => provider.GetUrl(It.IsAny<UmbracoContext>(), It.IsAny<int>(), It.IsAny<Uri>(), It.IsAny<UrlProviderMode>()))
                    .Returns("/hello/world/1234");

                var membershipHelper = new MembershipHelper(umbCtx, Mock.Of<MembershipProvider>(), Mock.Of<RoleProvider>());

                var mockedTypedContent = Mock.Of<IPublishedContentQuery>();

                var umbHelper = new UmbracoHelper(umbCtx,
                    Mock.Of<IPublishedContent>(),
                    mockedTypedContent,
                    Mock.Of<ITagQuery>(),
                    Mock.Of<IDataTypeService>(),
                    Mock.Of<ICultureDictionary>(),
                    Mock.Of<IUmbracoComponentRenderer>(),
                    membershipHelper,
                    new ServiceContext(), // fixme 'course that won't work
                    CacheHelper.NoCache);

                return CreateController(controllerType, request, umbHelper);
            }
            //default
            return base.Create(request, controllerDescriptor, controllerType);
        }

        protected abstract ApiController CreateController(Type controllerType, HttpRequestMessage msg, UmbracoHelper helper);
    }
}