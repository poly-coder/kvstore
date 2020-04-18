namespace PolyCoder.KVStore.Client

open PolyCoder
open PolyCoder.KVStore.Abstractions
open PolyCoder.KVStore.Protobuf
open Google.Protobuf
open Grpc.Core

[<AutoOpen>]
module ServiceWrapper =
  let createKeyValueStore
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
