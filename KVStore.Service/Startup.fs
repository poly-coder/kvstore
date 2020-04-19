namespace PolyCoder.KVStore.Service

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Configuration
open PolyCoder.KVStore.Protobuf.ServiceImpl
open PolyCoder.KVStore.Abstractions

type Startup(configRoot: IConfiguration) =

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    member this.ConfigureServices(services: IServiceCollection) =
        services.AddGrpc() |> ignore

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

        services.AddSingleton<KeyValueStoreServiceImplOptions>(fun (provider: IServiceProvider) ->
            let result: KeyValueStoreServiceImplOptions = {
                kvstore = provider.GetService<KeyValueStore<byte[], byte[]>>()
            }
            result
        ) |> ignore

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member this.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        if env.IsDevelopment() then
            app.UseDeveloperExceptionPage() |> ignore

        app.UseRouting() |> ignore

        app.UseEndpoints(fun endpoints ->
            endpoints.MapGrpcService<KeyValueStoreServiceImpl>() |> ignore
        ) |> ignore
