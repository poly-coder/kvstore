namespace PolyCoder.KVStore.Protobuf

open PolyCoder
open PolyCoder.KVStore.Abstractions
open PolyCoder.KVStore.Protobuf
open Google.Protobuf
open Grpc.Core
open FSharp.Control.Tasks.V2

[<AutoOpen>]
module ServiceWrapper =
  let createKeyValueStoreWrapper
    (service: KeyValueStoreService.KeyValueStoreServiceClient)
    : KeyValueStore<byte[], byte[]> =
    
    let inline ofBytes (bytes: byte[]) = ByteString.CopyFrom(bytes)
    let inline toBytes (bytes: ByteString) = bytes.ToByteArray()

    let callOptions() = async {
      let! cancel = Async.CancellationToken
      return CallOptions(cancellationToken = cancel)
    }

    let store key value = async {
      let request = StoreRequest()
      request.Key <- ofBytes key
      request.Value <- ofBytes value

      let! options = callOptions()

      let! _reply = service.StoreAsync(request, options).ResponseAsync |> Async.AwaitTask
      return ()
    }

    let remove key = async {
      let request = RemoveRequest()
      request.Key <- ofBytes key

      let! options = callOptions()

      let! _reply = service.RemoveAsync(request, options).ResponseAsync |> Async.AwaitTask
      return ()
    }

    let retrieve key = async {
      let request = RetrieveRequest()
      request.Key <- ofBytes key

      let! options = callOptions()

      let! reply = service.RetrieveAsync(request, options).ResponseAsync |> Async.AwaitTask
      if reply.Found then
        return Some (toBytes reply.Value)
      else
        return None
    }

    KeyValueStore.assemble store remove retrieve

module ServiceImpl =
  type KeyValueStoreServiceImplOptions = {
    kvstore: KeyValueStore<byte[], byte[]>
  }

  type KeyValueStoreServiceImpl(options: KeyValueStoreServiceImplOptions) =
    inherit KeyValueStoreService.KeyValueStoreServiceBase()
    
    let ofBytes (bytes: byte[]) = ByteString.CopyFrom(bytes)
    let toBytes (bytes: ByteString) = bytes.ToByteArray()

    override _.Store(request, context) = task {
      do! options.kvstore.store (toBytes request.Key) (toBytes request.Value)
          |> Async.StartAsTask
      return StoreReply()
    }

    override _.Remove(request, context) = task {
      do! options.kvstore.remove (toBytes request.Key)
          |> Async.StartAsTask
      return RemoveReply()
    }

    override _.Retrieve(request, context) = task {
      let! response =
        options.kvstore.retrieve (toBytes request.Key)
        |> Async.StartAsTask

      let reply = RetrieveReply()

      match response with
        | Some bytes ->
          reply.Found <- true
          reply.Value <- ofBytes bytes
        | None ->
          reply.Found <- false

      return reply
    }
