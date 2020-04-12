namespace PolyCoder.KVStore.Azure.Blobs

open PolyCoder
open PolyCoder.System
open PolyCoder.KVStore.Abstractions
open Azure
open Azure.Storage.Blobs
open System.IO

module KeyValueStore =
  [<CLIMutable>]
  type CreateKeyValueStoreOptions = {
    container: Lazy<Async<BlobContainerClient>>
  }

  let create (options: CreateKeyValueStoreOptions) : KeyValueStore<string, byte[]> =
    let store key value = async {
      let! container = options.container.Value
      let! cancel = Async.CancellationToken
      
      let blob = container.GetBlobClient key

      use content = new MemoryStream(value: byte[])

      let! _response = blob.UploadAsync(content, cancel) |> Async.AwaitTask

      return ()
    }

    let remove key = async {
      let! container = options.container.Value
      let! cancel = Async.CancellationToken
      
      let blob = container.GetBlobClient key

      let! _response = blob.DeleteIfExistsAsync(cancellationToken = cancel) |> Async.AwaitTask

      return ()      
    }

    let retrieve key = async {
      let! container = options.container.Value
      let! cancel = Async.CancellationToken
      
      let blob = container.GetBlobClient key
      
      use content = new MemoryStream()

      try

        let! response = blob.DownloadAsync(cancel) |> Async.AwaitTask

        do! response.Value.Content.CopyToAsync(content, 0x1000, cancel) |> Async.AwaitTask

        return Some <| content.ToArray()

      with
      | :? RequestFailedException as exn
        when exn.Status = 404 ->
        return None
      | exn ->
        return Exn.reraise exn
    }

    KeyValueStore.assemble store remove retrieve

  module FromConfig =

    type CreateKeyValueStoreConfig = {
      connectionString: string
      container: string
      doNotCreateContainer: bool
      accessType: Models.PublicAccessType
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

      { container = lazy (Async.toPromise (getContainer ())) }

    let create (config: CreateKeyValueStoreConfig) : KeyValueStore<byte[], byte[]> =
      let options = optionsFromConfig config

      let kvstore = create options

      kvstore |> KeyValueStore.convertKeys utf8ToString
