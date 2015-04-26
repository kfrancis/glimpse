using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Web;
using Glimpse.AspNet.Extensibility;
using Glimpse.Core.Extensibility;
using SimpleInjector;
using SimpleInjector.Advanced;
using SimpleInjector.Diagnostics;
using CurrentRequestCache = System.Runtime.CompilerServices.ConditionalWeakTable<System.Web.HttpContext, System.Collections.Generic.List<SimpleInjector.Advanced.InstanceInitializationData>>;

namespace Glimpse.SimpleInjector
{
    public class SimpleInjectorTab : AspNetTab, IDocumentation
    {
        // By using a weak table, the stored items in the list will be garbage collected automatically, and
        // by using the HttpContext as key, we allow each request to retrieve its own instances.
        private static readonly CurrentRequestCache ResolvedInstances = new CurrentRequestCache();
        private static readonly CurrentRequestCache CreatedInstances = new CurrentRequestCache();
        private static readonly ConcurrentDictionary<InstanceProducer, string> ObjectGraphs =
            new ConcurrentDictionary<InstanceProducer, string>();

        private static IEnumerable<object> DiagnosticWarnings;
        private static IEnumerable<object> KnownRegistrations;
        private static IEnumerable<object> RootRegistrations;

        public static void ConfigureGlimpseAndVerifyContainer(Container container)
        {
            container.Options.RegisterResolveInterceptor(CollectResolvedInstance, Always);
            container.RegisterInitializer(CollectCreatedInstance, Always);
            
            container.Verify(VerificationOption.VerifyOnly);

            DiagnosticWarnings = GetDiagnosticWarnings(container).ToArray();
            RootRegistrations = GetRootRegistrations(container).ToArray();
            KnownRegistrations = GetContainerRegistrations(container).ToArray();
        }

        public override string Name
        {
            get { return "Simple Injector"; }
        }

        public string DocumentationUri
        {
            get { return "https://simpleinjector.org/documentation"; }
        }

        public override object GetData(ITabContext context)
        {
            return new
            {
                resolvedInstances = GetResolvedItemsForCurrentRequest(),
                createdInstances = GetCreatedItemsForCurrentRequest(),
                diagnosticWarnings = DiagnosticWarnings,
                rootRegistrations = RootRegistrations,
                registrations = KnownRegistrations,
            };
        }

        private static IEnumerable<object> GetResolvedItemsForCurrentRequest()
        {
            return (
                from data in GetListForCurrentRequest(ResolvedInstances)
                select new
                {
                    service = data.Context.Producer.ServiceType.ToFriendlyName(),
                    implementation = data.Context.Registration.ImplementationType.ToFriendlyName(),
                    lifestyle = data.Context.Registration.Lifestyle.Name,
                    graph = VisualizeObjectGraph(data.Context.Producer)
                })
                .ToArray();
        }

        private static IEnumerable<object> GetCreatedItemsForCurrentRequest()
        {
            return (
                from data in GetListForCurrentRequest(CreatedInstances)
                select new
                {
                    service = data.Context.Producer.ServiceType.ToFriendlyName(),
                    implementation = data.Context.Registration.ImplementationType.ToFriendlyName(),
                    lifestyle = data.Context.Registration.Lifestyle.Name,
                })
                .ToArray();
        }

        private static IEnumerable<object> GetDiagnosticWarnings(Container container)
        {
            return
                from result in Analyzer.Analyze(container)
                where result.DiagnosticType != DiagnosticType.ContainerRegisteredComponent
                where result.DiagnosticType != DiagnosticType.SingleResponsibilityViolation
                select BuildResult(result);
        }

        private static object BuildGroup(DiagnosticGroup group)
        {
            return new
            {
                Name = group.Name,
                Items = group.Children.Select(BuildGroup).Concat(group.Results.Select(BuildResult)).ToArray()
            };
        }

        private static object BuildResult(DiagnosticResult result)
        {
            return new { Type = result.ServiceType.ToFriendlyName(), result.Description };
        }

        private static DiagnosticGroup GetRootGroup(DiagnosticGroup group)
        {
            while (group.Parent != null)
            {
                group = group.Parent;
            }

            return group;
        }

        private static IEnumerable<object> GetContainerRegistrations(Container container)
        {
            return
                from producer in container.GetCurrentRegistrations()
                orderby producer.ServiceType.ToFriendlyName()
                select new
                {
                    service = producer.ServiceType.ToFriendlyName(),
                    implementation = producer.Registration.ImplementationType.ToFriendlyName(),
                    lifestyle = producer.Registration.Lifestyle.Name,
                };
        }

        private static IEnumerable<object> GetRootRegistrations(Container container)
        {
            return
                from producer in container.GetRootRegistrations()
                orderby producer.ServiceType.ToFriendlyName()
                select new
                {
                    service = producer.ServiceType.ToFriendlyName(),
                    graph = VisualizeObjectGraph(producer),
                };
        }

        private static object CollectResolvedInstance(InitializationContext context, Func<object> instanceProducer)
        {
            object instance = instanceProducer();

            GetListForCurrentRequest(ResolvedInstances).Add(new InstanceInitializationData(context, instance));

            return instance;
        }

        private static void CollectCreatedInstance(InstanceInitializationData instance)
        {
            GetListForCurrentRequest(CreatedInstances).Add(instance);
        }

        private static List<InstanceInitializationData> GetListForCurrentRequest(CurrentRequestCache dictionary)
        {
            HttpContext currentRequest = HttpContext.Current;

            if (currentRequest == null)
            {
                return new List<InstanceInitializationData>();
            }

            lock (dictionary)
            {
                List<InstanceInitializationData> currentRequestInstances;

                if (!dictionary.TryGetValue(currentRequest, out currentRequestInstances))
                {
                    dictionary.Add(currentRequest,
                        currentRequestInstances = new List<InstanceInitializationData>(capacity: 32));
                }

                return currentRequestInstances;
            }
        }

        private static string VisualizeObjectGraph(InstanceProducer producer)
        {
            return ObjectGraphs.GetOrAdd(producer, p => p.VisualizeObjectGraph());
        }

        private static bool Always(InitializationContext context)
        {
            return true;
        }
    }
}