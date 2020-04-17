namespace PolyCoder.KVStore.Service

open PolyCoder
open Microsoft.AspNetCore.Mvc
open FSharp.Control.Tasks.V2
open System
open System.IO
open System.Text
open System.Net.Mime

[<AllowNullLiteral>]
[<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Method, Inherited = false, AllowMultiple = false)>]
type SwaggerOperationTagsAttribute([<ParamArray>] tags: string[]) =
    inherit Attribute()

    member this.Tags: string[] = tags

[<CLIMutable>]
type StoreRequestDao = {
    key: string
    value: string
}

[<CLIMutable>]
type StoreValueRequestDao = {
    value: string
}

[<CLIMutable>]
type RetrieveRequestDao = {
    key: string
}

[<CLIMutable>]
type RemoveRequestDao = {
    key: string
}

[<CLIMutable>]
type RetrieveResponseDao = {
    value: string
}

[<ApiController; Route("api/kvstore")>]
type StoreController(kvstore: KVStoreService) =
    inherit ControllerBase()

    let ok value = OkObjectResult(value) :> IActionResult
    let okStatus() = OkResult() :> IActionResult
    let notFound = NotFoundResult() :> IActionResult
    let textContent text =
        let content = ContentResult()
        content.Content <- text
        content.ContentType <- MediaTypeNames.Text.Plain
        content :> IActionResult
    let binaryContent bytes =
        let content = FileContentResult(bytes, MediaTypeNames.Application.Octet)
        content :> IActionResult

    // Json
    [<HttpGet("json/{key}", Name = "JsonRetrieve")>]
    [<SwaggerOperationTags("Retrieve", "Json")>]
    [<Consumes(MediaTypeNames.Application.Json)>]
    [<ProducesResponseType(typeof<RetrieveResponseDao>, 200)>]
    [<ProducesResponseType(404)>]
    member this.JsonRetrieve(key: string) = task {
        let keyBytes = System.stringToUtf8 key
        match! kvstore.retrieve keyBytes with
        | Some value ->
            let dao: RetrieveResponseDao = {
              value = System.utf8ToString value
            }
            return ok dao
        | None ->
            return notFound
    }

    [<HttpPost("json", Name = "JsonStore")>]
    [<HttpPut("json", Name = "JsonStorePut")>]
    [<SwaggerOperationTags("Store", "Json")>]
    [<ProducesResponseType(200)>]
    [<ProducesResponseType(500)>]
    member this.JsonStore([<FromBody>] body: StoreRequestDao) = task {
        let keyBytes = System.stringToUtf8 body.key
        let valueBytes = System.stringToUtf8 body.value
        do! kvstore.store keyBytes valueBytes
        return okStatus()
    }

    [<HttpDelete("json/{key}", Name = "JsonDelete")>]
    [<SwaggerOperationTags("Remove", "Json")>]
    [<ProducesResponseType(200)>]
    [<ProducesResponseType(500)>]
    member this.JsonDelete(key: string) = task {
        let keyBytes = System.stringToUtf8 key
        do! kvstore.remove keyBytes
        return okStatus()
    }

    // Text
    [<HttpGet("text/{key}", Name = "TextRetrieve")>]
    [<SwaggerOperationTags("Retrieve", "Text")>]
    [<Consumes(MediaTypeNames.Application.Json)>]
    [<ProducesResponseType(200)>]
    [<ProducesResponseType(404)>]
    member this.TextRetrieve(key: string) = task {
        let keyBytes = System.stringToUtf8 key
        match! kvstore.retrieve keyBytes with
        | Some value ->
            return textContent (System.utf8ToString value)
        | None ->
            return notFound
    }

    [<HttpPost("text/{key}", Name = "TextStore")>]
    [<HttpPut("text/{key}", Name = "TextStorePut")>]
    [<SwaggerOperationTags("Store", "Text")>]
    [<Consumes(MediaTypeNames.Text.Plain)>]
    [<ProducesResponseType(200)>]
    [<ProducesResponseType(500)>]
    member this.TextStore(key: string) = task {
        let keyBytes = System.stringToUtf8 key
        use reader = new StreamReader(this.Request.Body, Encoding.UTF8)
        let! value = reader.ReadToEndAsync()
        let valueBytes = System.stringToUtf8 value
        do! kvstore.store keyBytes valueBytes
        return okStatus()
    }

    [<HttpDelete("text", Name = "TextDelete")>]
    [<SwaggerOperationTags("Delete", "Text")>]
    [<Consumes(MediaTypeNames.Text.Plain)>]
    [<ProducesResponseType(200)>]
    [<ProducesResponseType(500)>]
    member this.TextDelete() = task {
        use reader = new StreamReader(this.Request.Body, Encoding.UTF8)
        let! key = reader.ReadToEndAsync()
        let keyBytes = System.stringToUtf8 key
        do! kvstore.remove keyBytes
        return okStatus()
    }

    // Binary
    [<HttpGet("binary/{key}", Name = "BinaryRetrieve")>]
    [<SwaggerOperationTags("Retrieve", "Binary")>]
    [<Consumes(MediaTypeNames.Application.Octet)>]
    [<ProducesResponseType(200)>]
    [<ProducesResponseType(404)>]
    member this.BinaryRetrieve(key: string) = task {
        let keyBytes = System.stringToUtf8 key
        match! kvstore.retrieve keyBytes with
        | Some value ->
            return binaryContent value
        | None ->
            return notFound
    }

    [<HttpPost("binary/{key}", Name = "BinaryStore")>]
    [<HttpPut("binary/{key}", Name = "BinaryStorePut")>]
    [<SwaggerOperationTags("Store", "Binary")>]
    [<Consumes(MediaTypeNames.Application.Octet)>]
    [<ProducesResponseType(200)>]
    [<ProducesResponseType(500)>]
    member this.BinaryStore(key: string) = task {
        let keyBytes = System.stringToUtf8 key
        use mem = new MemoryStream()
        do! this.Request.Body.CopyToAsync(mem)
        let valueBytes = mem.ToArray()
        do! kvstore.store keyBytes valueBytes
        return okStatus()
    }

    [<HttpDelete("binary", Name = "BinaryDelete")>]
    [<SwaggerOperationTags("Delete", "Binary")>]
    [<Consumes(MediaTypeNames.Application.Octet)>]
    [<ProducesResponseType(200)>]
    [<ProducesResponseType(500)>]
    member this.BinaryDelete() = task {
        use mem = new MemoryStream()
        do! this.Request.Body.CopyToAsync(mem)
        let keyBytes = mem.ToArray()
        do! kvstore.remove keyBytes
        return okStatus()
    }
