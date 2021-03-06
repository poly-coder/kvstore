﻿namespace PolyCoder.KVStore.Service

open PolyCoder.KVStore.Abstractions
open Microsoft.Extensions.Configuration
open System
open System.Reflection
open System.IO
open System.Runtime.Loader

type KVStoreService = KeyValueStore<byte[], byte[]>

module KVStoreService =
  [<Literal>]
  let FromConfigMode = "create"

  type AssemblySource =
    | Local = 0
    | File = 1

  [<CLIMutable>]
  type FromConfigOptions = {
    Assembly: string
    AssemblyFile: string
    Type: string
    Method: string
  }
  
  let fromConfig (config: #IConfiguration) (options: FromConfigOptions) : KVStoreService = 
    let assembly =
        if String.IsNullOrEmpty options.AssemblyFile |> not then
            let folder = Path.GetDirectoryName(options.AssemblyFile)
            let result = ref Unchecked.defaultof<_>
            Directory.EnumerateFiles(folder, "*.dll", SearchOption.TopDirectoryOnly)
            |> Seq.iter (fun fileName ->
                try
                    let loaded = AssemblyLoadContext.Default.LoadFromAssemblyPath(fileName)
                    if options.AssemblyFile = fileName then result := loaded
                with
                | _exn -> ()
            )
            !result
        elif String.IsNullOrEmpty options.Assembly |> not then
            Assembly.Load options.Assembly
        else
            invalidOp "Unknown assembly source. Specify one of Assembly or AssemblyFile parameters"
    let type' = assembly.GetType(options.Type)
    let method = type'.GetMethod(options.Method)

    let optionsType = method.GetParameters().[0].ParameterType
    let createOptions = config.Get(optionsType)
    let kvstore = method.Invoke(null, [| createOptions |]) :?> KeyValueStore<byte[], byte[]>
    
    kvstore
