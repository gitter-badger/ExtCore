﻿// Copyright © 2015 Dmitry Sikorsky. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExtCore.Infrastructure;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.FileProviders;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Mvc.Infrastructure;
using Microsoft.AspNet.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;

namespace ExtCore.WebApplication
{
  public class Startup
  {
    protected IConfigurationRoot configurationRoot;

    private string applicationBasePath;
    private IAssemblyLoaderContainer assemblyLoaderContainer;
    private IAssemblyLoadContextAccessor assemblyLoadContextAccessor;

    public Startup(IHostingEnvironment hostingEnvironment, IApplicationEnvironment applicationEnvironment, IAssemblyLoaderContainer assemblyLoaderContainer, IAssemblyLoadContextAccessor assemblyLoadContextAccessor)
    {
      this.applicationBasePath = applicationEnvironment.ApplicationBasePath;
      this.assemblyLoaderContainer = assemblyLoaderContainer;
      this.assemblyLoadContextAccessor = assemblyLoadContextAccessor;
    }

    public virtual void ConfigureServices(IServiceCollection services)
    {
      IEnumerable<Assembly> assemblies = AssemblyManager.LoadAssemblies(
        this.applicationBasePath.Substring(0, this.applicationBasePath.LastIndexOf("src")) + "artifacts\\bin\\Extensions",
        this.assemblyLoaderContainer,
        this.assemblyLoadContextAccessor
      );

      ExtensionManager.SetAssemblies(assemblies);
      services.AddCaching();
      services.AddSession();
      services.AddMvc().AddPrecompiledRazorViews(ExtensionManager.Assemblies.ToArray());
      services.Configure<RazorViewEngineOptions>(options =>
        {
          options.FileProvider = this.GetFileProvider(this.applicationBasePath);
        }
      );

      foreach (IExtension extension in ExtensionManager.Extensions)
      {
        extension.SetConfigurationRoot(this.configurationRoot);
        extension.ConfigureServices(services);
      }

      services.AddTransient<DefaultAssemblyProvider>();
      services.AddTransient<IAssemblyProvider, ExtensionAssemblyProvider>();
    }

    public virtual void Configure(IApplicationBuilder applicationBuilder, IHostingEnvironment hostingEnvironment)
    {
      applicationBuilder.UseSession();
      applicationBuilder.UseStaticFiles();

      foreach (IExtension extension in ExtensionManager.Extensions)
        extension.Configure(applicationBuilder);

      applicationBuilder.UseMvc(routeBuilder =>
        {
          routeBuilder.MapRoute(name: "Resource", template: "resource", defaults: new { controller = "Resource", action = "Index" });

          foreach (IExtension extension in ExtensionManager.Extensions)
            extension.RegisterRoutes(routeBuilder);
        }
      );
    }

    public IFileProvider GetFileProvider(string path)
    {
      IEnumerable<IFileProvider> fileProviders = new IFileProvider[] { new PhysicalFileProvider(path) };

      return new CompositeFileProvider(
        fileProviders.Concat(
          ExtensionManager.Assemblies.Select(a => new EmbeddedFileProvider(a, a.GetName().Name))
        )
      );
    }
  }
}