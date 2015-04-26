# Glimpse.SimpleInjector

This simple [Glimpse](http://getglimpse.com/) plug-in shows the current [Simple Injector](https://simpleinjector.org) container configuration:

- **Resolved Instances** lists the types that were requested explicitly during the request.
- **Created Instances** lists the types that were created by the container during the request.
- **Diagnostic Warnings** reports any diagnostic warnings found in the current configuration.
- **Root Registrations** shows all root objects with their complete dependency graph.
- **Registrations** lists all of the registrations ordered by the service name.

To use this plug-in you need to pass in the `container` instance *before verification* to allow the plug-in to add it's own hooks into the container and then verify the configuration for itself

```csharp
#if DEBUG
    Glimpse
        .SimpleInjector
        .SimpleInjectorTab
        .ConfigureGlimpseAndVerifyContainer(container);
#else
    container.Verify();
#endif
```

