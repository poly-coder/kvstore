namespace PolyCoder.KVStore.Protobuf

open PolyCoder
open PolyCoder.KVStore.Abstractions
open PolyCoder.KVStore.Protobuf
open Google.Protobuf
open FSharp.Control.Tasks.V2

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
