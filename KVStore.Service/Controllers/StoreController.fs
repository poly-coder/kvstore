namespace PolyCoder.KVStore.Service

open PolyCoder
open Microsoft.AspNetCore.Mvc
open FSharp.Control.Tasks.V2

[<ApiController; Route("api/store")>]
type StoreController(kvstore: KVStoreService) =
    inherit ControllerBase()

    [<HttpGet("{key}")>]
    member this.Get(key: string) = task {
        let keyBytes = System.stringToUtf8 key
        match! kvstore.retrieve keyBytes with
        | Some value ->
            let text = System.utf8ToString value
            return OkObjectResult(text) :> IActionResult

        | None ->
            return NotFoundResult() :> IActionResult
    }