#region Copyright Simple Injector Contributors
/* The Simple Injector is an easy-to-use Inversion of Control library for .NET
 * 
 * Copyright (c) 2015-2017 Simple Injector Contributors
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
 * associated documentation files (the "Software"), to deal in the Software without restriction, including 
 * without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
 * copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the 
 * following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all copies or substantial 
 * portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT 
 * LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO 
 * EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER 
 * IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE 
 * USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
#endregion

namespace Glimpse.SimpleInjector
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Web;
    using Glimpse.AspNet.Extensibility;
    using Glimpse.Core.Extensibility;
    using global::SimpleInjector;
    using global::SimpleInjector.Advanced;
    using global::SimpleInjector.Diagnostics;

    public class SimpleInjectorTab : AspNetTab, IDocumentation
    {
        // By using a weak table, the stored items in the list will be garbage collected automatically, and
        // by using the HttpContext as key, we allow each request to retrieve its own instances.
        private static readonly ConditionalWeakTable<HttpContext, List<InitializationData>> ResolvedInstances = 
            new ConditionalWeakTable<HttpContext, List<InitializationData>>();
        private static readonly ConditionalWeakTable<HttpContext, List<InstanceInitializationData>> CreatedInstances = 
            new ConditionalWeakTable<HttpContext, List<InstanceInitializationData>>();
        private static readonly ConcurrentDictionary<InstanceProducer, string> ObjectGraphs =
            new ConcurrentDictionary<InstanceProducer, string>();

        private static object Container;
        private static object[] DiagnosticWarnings;
        private static object[] KnownRegistrations;
        private static object[] RootRegistrations;

        public static void ConfigureGlimpseAndVerifyContainer(Container container)
        {
            if (Container != null)
            {
                throw new InvalidOperationException("This method can only be called once.");
            }

            container.Options.RegisterResolveInterceptor(CollectResolvedInstance, Always);
            container.RegisterInitializer(CollectCreatedInstance, Always);
            
            container.Verify(VerificationOption.VerifyOnly);

            DiagnosticWarnings = GetDiagnosticWarnings(container).ToArray();
            RootRegistrations = GetRootRegistrations(container).ToArray();
            KnownRegistrations = GetContainerRegistrations(container).ToArray();

            Container = new
            {
                version = typeof(Container).Assembly.GetName().Version.ToString(),
                options = container.Options.ToString(),
            };
        }

        public override string Name => "Simple Injector";

        public string DocumentationUri => "https://simpleinjector.org/documentation";

        public override object GetData(ITabContext context)
        {
            return new
            {
                container = Container,
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
                    service = GetServiceType(data.Context).ToFriendlyName(),
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
                let registration = data.Context.Registration
                select new
                {
                    implementation = registration.ImplementationType.ToFriendlyName(),
                    lifestyle = registration.Lifestyle.Name,
                })
                .ToArray();
        }

        private static Type GetServiceType(InitializationContext context) =>
            context.Producer?.ServiceType ?? context.Registration.ImplementationType;

        private static IEnumerable<object> GetDiagnosticWarnings(Container container)
        {
            return
                from result in Analyzer.Analyze(container)
                where result.Severity != DiagnosticSeverity.Information
                select new 
                {
                    type = result.DiagnosticType.ToString(),
                    service = result.ServiceType.ToFriendlyName(),
                    description = result.Description,
                };
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
            GetListForCurrentRequest(ResolvedInstances).Add(new InitializationData(context, instance));
            return instance;
        }

        private static void CollectCreatedInstance(InstanceInitializationData instance)
        {
            GetListForCurrentRequest(CreatedInstances).Add(instance);
        }

        private static List<T> GetListForCurrentRequest<T>(ConditionalWeakTable<HttpContext, List<T>> dictionary)
        {
            HttpContext currentRequest = HttpContext.Current;

            if (currentRequest == null)
            {
                return new List<T>();
            }

            lock (dictionary)
            {
                List<T> currentRequestInstances;

                if (!dictionary.TryGetValue(currentRequest, out currentRequestInstances))
                {
                    dictionary.Add(currentRequest,
                        currentRequestInstances = new List<T>(capacity: 32));
                }

                return currentRequestInstances;
            }
        }

        private static string VisualizeObjectGraph(InstanceProducer producer) =>
            ObjectGraphs.GetOrAdd(producer, p => p.VisualizeObjectGraph());

        private static bool Always(InitializationContext context) => true;
        private static bool Always(InitializerContext context) => true;

        private struct InitializationData
        {
            public readonly InitializationContext Context;
            public readonly object Instance;

            public InitializationData(InitializationContext context, object instance)
            {
                this.Context = context;
                this.Instance = instance;
            }
        }
    }
}