using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Glimpse.AspNet.Extensibility;
using Glimpse.AspNet.Extensions;
using Glimpse.Core.Extensibility;
using Glimpse.Core.Tab.Assist;
using SimpleInjector;
using SimpleInjector.Diagnostics;

namespace Glimpse.SimpleInjector
{
    public class SimpleInjectorTab : AspNetTab, IDocumentation
    {
        private static List<object> resolved = new List<object>();
        private static IEnumerable<object> diagnosticResults = null;
        private static IEnumerable<object> services = null;
        private static IEnumerable<object> implementations = null;
        private static IEnumerable<object> rootRegistrations = null;

        public static void ConfigureGlimpseAndVerifyContainer(Container container)
        {
            container.RegisterInitializer<object>(o => SimpleInjectorTab.resolved.Add(o));
            container.Verify();
            SimpleInjectorTab.resolved.Clear();
            SimpleInjectorTab.SetupDiagnosticResults(container);
            SimpleInjectorTab.SetupServices(container);
            SimpleInjectorTab.SetupImplementations(container);
            SimpleInjectorTab.SetupRootRegistrations(container);
        }

        public override object GetData(ITabContext context)
        {
            var result = new
            {
                resolvedTypes = SimpleInjectorTab.ResolvedItems(),
                diagnostics = SimpleInjectorTab.diagnosticResults,
                services = SimpleInjectorTab.services,
                implementations = SimpleInjectorTab.implementations,
                rootRegistrations = SimpleInjectorTab.rootRegistrations
            };

            return result;
        }

        public string DocumentationUri
        {
            get
            {
                return "https://simpleinjector.org/documentation";
            }
        }

        public override string Name
        {
            get
            {
                return "SimpleInjector";
            }
        }

        private static object ResolvedItems()
        {
            var resolves = (
                from instance in SimpleInjectorTab.resolved
                group instance by instance.GetType().AsFriendlyName()
                into g
                select new
                {
                    key = g.Key,
                    noOfResolves = g.Count()
                })
                .ToList();

            SimpleInjectorTab.resolved.Clear();

            return resolves;
        }

        private static void SetupDiagnosticResults(Container container)
        {
            var result = Analyzer.Analyze(container);
            SimpleInjectorTab.diagnosticResults =
                from detail in result
                select new
                {
                    service = detail.ServiceType.AsFriendlyName(),
                    description = detail.Description
                };
        }

        private static void SetupServices(Container container)
        {
            SimpleInjectorTab.services =
                from registration in container.GetCurrentRegistrations()
                orderby registration.ServiceType.AsFriendlyName()
                select new
                {
                    service = registration.ServiceType.AsFriendlyName(),
                    implementation = registration.Registration.ImplementationType.AsFriendlyName(),
                    lifestyle = registration.Registration.Lifestyle.Name,
                };
        }

        private static void SetupImplementations(Container container)
        {
            SimpleInjectorTab.implementations =
                from registration in container.GetCurrentRegistrations()
                orderby registration.Registration.ImplementationType.AsFriendlyName()
                select new
                {
                    service = registration.ServiceType.AsFriendlyName(),
                    implementation = registration.Registration.ImplementationType.AsFriendlyName(),
                    lifestyle = registration.Registration.Lifestyle.Name,
                };
        }

        private static void SetupRootRegistrations(Container container)
        {
            SimpleInjectorTab.rootRegistrations =
                from registration in container.GetRootRegistrations()
                orderby registration.ServiceType.AsFriendlyName()
                select new
                {
                    service = registration.ServiceType.AsFriendlyName(),
                    graph = registration.VisualizeObjectGraph().Replace('+', '.'),
                };
        }
    }
}
