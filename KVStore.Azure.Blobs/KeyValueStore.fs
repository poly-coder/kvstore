namespace PolyCoder.KVStore.Azure.Blobs

open PolyCoder
open PolyCoder.System
open PolyCoder.KVStore.Abstractions
open Azure
open Azure.Storage.Blobs
open System
open System.IO
open System.Reflection

module Exn =
  let findInner<'a when 'a :> exn> (exn: exn) : 'a option =
    let rec find (e: exn) =
      let fromSeq source = 
        source
          |> Seq.map find
          |> Seq.tryFind Option.isSome
          |> Option.flatten
      match e with
      | :? 'a as exn -> Some exn
      | :? TargetInvocationException as targetExn -> find targetExn.InnerException
      | :? AggregateException as aggExn -> fromSeq aggExn.InnerExceptions
      | _ -> None

    find exn

module KeyValueStore =
  [<CLIMutable>]
  type CreateKeyValueStoreOptions = {
    container: Lazy<Async<BlobContainerClient>>
    subFolder: string
  }

  let create (options: CreateKeyValueStoreOptions) : KeyValueStore<string, byte[]> =
    let makeKey =
      if String.IsNullOrEmpty options.subFolder then id
      else sprintf "%s/%s" options.subFolder

    let store key value = async {
      let! container = options.container.Value
      let! cancel = Async.CancellationToken

      let blob = makeKey key |> container.GetBlobClient

      use content = new MemoryStream(value: byte[])

      let! _response = blob.UploadAsync(content, cancel) |> Async.AwaitTask

      return ()
    }

    let remove key = async {
      let! container = options.container.Value
      let! cancel = Async.CancellationToken
      
      let blob = makeKey key |> container.GetBlobClient

      let! _response = blob.DeleteIfExistsAsync(cancellationToken = cancel) |> Async.AwaitTask

      return ()      
    }

    let retrieve key = async {
      let! container = options.container.Value
      let! cancel = Async.CancellationToken
      
      let blob = makeKey key |> container.GetBlobClient
      
      use content = new MemoryStream()

      try

        let! response = blob.DownloadAsync(cancel) |> Async.AwaitTask

        do! response.Value.Content.CopyToAsync(content, 0x1000, cancel) |> Async.AwaitTask

        return Some <| content.ToArray()

      with
      | exn ->
        match Exn.findInner<RequestFailedException> exn with
        | Some exn when exn.Status = 404 ->
          return None
        | _ ->
          return Exn.reraise exn
    }

    KeyValueStore.assemble store remove retrieve

  module FromConfig =

    [<CLIMutable>]
    type CreateKeyValueStoreConfig = {
      connectionString: string
      container: string
      doNotCreateContainer: bool
      accessType: Models.PublicAccessType
      subFolder: string
    }

    let optionsFromConfig (config: CreateKeyValueStoreConfig) : CreateKeyValueStoreOptions =
      let getContainer() = async {
        let container = new BlobContainerClient(config.connectionString, config.container)

        if not config.doNotCreateContainer then
          let! _containerInfo =
            container.CreateIfNotExistsAsync(publicAccessType = config.accessType)
            |> Async.AwaitTask
          ()

        return container
      }

      { container = lazy (getContainer ())
        subFolder = config.subFolder }

    let create (config: CreateKeyValueStoreConfig) : KeyValueStore<byte[], byte[]> =
      let options = optionsFromConfig config

      let kvstore = create options

      kvstore |> KeyValueStore.convertKeys utf8ToString
