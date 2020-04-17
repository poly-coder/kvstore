namespace PolyCoder.KVStore.Service

open System
open System.Reflection
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.OpenApi.Models
open System.Collections.Generic
open Microsoft.AspNetCore.Mvc.Controllers

type Startup(configRoot: IConfiguration) =

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    member this.ConfigureServices(services: IServiceCollection) =
        services.AddControllers() |> ignore

        services.AddSwaggerGen(fun config ->
            let apiInfo = OpenApiInfo()
            apiInfo.Title <- "Key-Value Store"
            apiInfo.Version <- "v0"
            config.SwaggerDoc("v0", apiInfo) |> ignore

            config.TagActionsBy(fun desc ->
                match desc.ActionDescriptor with
                | :? ControllerActionDescriptor as desc ->
                    let controllerAttr = desc.ControllerTypeInfo.GetCustomAttribute<SwaggerOperationTagsAttribute>()
                    let actionAttr = desc.MethodInfo.GetCustomAttribute<SwaggerOperationTagsAttribute>()
                    seq {
                        if isNull controllerAttr |> not then
                            yield! controllerAttr.Tags
                        if isNull actionAttr |> not then
                            yield! actionAttr.Tags
                    }
                | _ -> Seq.empty
                |> Seq.distinct
                |> Seq.toArray
                :> IList<_>
            )
        ) |> ignore

        services.AddSingleton<KVStoreService>(fun (provider: IServiceProvider) ->
            let mode = configRoot.GetValue("KVStore:mode")

            match mode with
            | KVStoreService.FromConfigMode ->
                let options = configRoot.GetSection("KVStore:options").Get<KVStoreService.FromConfigOptions>()
                let service = KVStoreService.fromConfig (configRoot.GetSection("KVStore:config")) options
                service

            | _ ->
                invalidOp (sprintf "Unknown KVStore mode: %s" mode)

        ) |> ignore

        ()

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member this.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        if env.IsDevelopment() then
            app.UseDeveloperExceptionPage() |> ignore

        app.UseSwagger() |> ignore

        app.UseSwaggerUI(fun config ->
            config.SwaggerEndpoint("/swagger/v0/swagger.json", "Key-Value Store API v0")
        ) |> ignore

        app.UseRouting() |> ignore

        app.UseEndpoints(fun endpoints ->
            endpoints.MapControllers() |> ignore

            // endpoints.MapGet("/", fun context -> context.Response.WriteAsync("Hello World!")) |> ignore
        ) |> ignore
