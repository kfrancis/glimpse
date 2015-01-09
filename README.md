# Glimpse.SimpleInjector

This simple Glimpse plug-in shows the current [Simple Injector](https://simpleinjector.org) container configuration:

- **Resolved Types** lists the types that were resolved for each service call
- **Diagnostics** reports any diagnostic errors or warnings found in the current configuration
- **Services** lists all of the registrations ordered by the service name (e.g. `ICommandHandler<UpdateData>`)
- **Implementations** lists all registrations ordered by the implementation name (e.g. `UpdateDataHandler`)
- **Root Registrations** shows all potential root objects with their complete dependency graph

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

