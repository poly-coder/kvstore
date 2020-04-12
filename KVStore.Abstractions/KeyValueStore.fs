namespace PolyCoder.KVStore.Abstractions

open PolyCoder

type StoreKeyValue<'key, 'value> = 'key -> 'value -> Async<unit>
type RemoveKey<'key> = 'key -> Async<unit>
type RetrieveValue<'key, 'value> = 'key -> Async<Option<'value>>

type KeyValueStore<'key, 'value> = {
  store: StoreKeyValue<'key, 'value>
  remove: RemoveKey<'key>
  retrieve: RetrieveValue<'key, 'value>
}

type KeyValueStoreCommand<'key, 'value> =
  | StoreKeyValue of 'key * 'value * ResultSink<unit>
  | RemoveKey of 'key * ResultSink<unit>
  | RetrieveValue of 'key * ResultSink<Option<'value>>

type KeyValueStoreProcessor<'key, 'value> =
  Sink<KeyValueStoreCommand<'key, 'value>>

module KeyValueStore =
  let assemble store remove retrieve : KeyValueStore<'key, 'value> =
    { store = store
      remove = remove
      retrieve = retrieve }

  let toProcessor (store: KeyValueStore<'key, 'value>) : KeyValueStoreProcessor<'key, 'value> =
    function
      | StoreKeyValue(key, value, sink) ->
        ResultSink.ofAsync sink (fun () -> store.store key value)

      | RemoveKey(key, sink) ->
        ResultSink.ofAsync sink (fun () -> store.remove key)

      | RetrieveValue(key, sink) ->
        ResultSink.ofAsync sink (fun () -> store.retrieve key)

  let ofProcessor (processor : KeyValueStoreProcessor<'key, 'value>) : KeyValueStore<'key, 'value> =
    let store key value =
      ResultSink.toAsync (fun sink -> processor(StoreKeyValue(key, value, sink)))

    let remove key =
      ResultSink.toAsync (fun sink -> processor(RemoveKey(key, sink)))

    let retrieve key =
      ResultSink.toAsync (fun sink -> processor(RetrieveValue(key, sink)))

    assemble store remove retrieve

  let inMemoryProcessor () : KeyValueStoreProcessor<'key, 'value> =
    let mailbox = MailboxProcessor.Start(fun mb ->
      let rec loop state = async {
        let! cmd = mb.Receive()

        match cmd with
          | StoreKeyValue(key, value, sink) ->
            let state' = state |> Map.add key value
            sink(Ok())
            return! loop state'

          | RemoveKey(key, sink) ->
            let state' = state |> Map.remove key
            sink(Ok())
            return! loop state'

          | RetrieveValue(key, sink) ->
            let result = state |> Map.tryFind key
            sink(Ok result)
            return! loop state
      }

      loop Map.empty
    )

    mailbox.Post
    
  let convertKeys toInnerKey (kvstore : KeyValueStore<'innerKey, 'value>) : KeyValueStore<'key, 'value> =
    let store key value =
      kvstore.store (toInnerKey key) value

    let remove key =
      kvstore.remove (toInnerKey key)

    let retrieve key =
      kvstore.retrieve (toInnerKey key)

    assemble store remove retrieve
    
  let convertValues toInnerValue fromInnerValue (kvstore : KeyValueStore<'key, 'innerValue>) : KeyValueStore<'key, 'value> =
    let store key value =
      kvstore.store key (toInnerValue value)

    let remove key =
      kvstore.remove key

    let retrieve key = async {
      let! result = kvstore.retrieve key
      return result |> Option.map fromInnerValue
    }

    assemble store remove retrieve
