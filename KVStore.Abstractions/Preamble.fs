module PolyCoder.Preamble

open System.Reflection
open System.Threading.Tasks

type Sink<'a> = 'a -> unit
type ResultSink<'a> = Sink<Result<'a, exn>>

module Sink =
  let ofAsync sink fn =
    async {
      let! result = fn()
      sink result
    } |> Async.Start

  let toAsync (processFn: Sink<Sink<'value>>) =
    let source = TaskCompletionSource()
    let sink value = source.TrySetResult(value) |> ignore
    processFn(sink)
    source.Task |> Async.AwaitTask

module ResultSink =
  let ofAsync sink fn =
    async {
      try
        let! result = fn()
        sink (Ok result)
      with
        exn -> sink (Error exn)
    } |> Async.Start

  let toAsync (processFn: Sink<ResultSink<'value>>) =
    let source = TaskCompletionSource()
    let sink = function
      | Ok value -> source.TrySetResult(value) |> ignore
      | Error exn -> source.TrySetException(exn: exn) |> ignore
    processFn(sink)
    source.Task |> Async.AwaitTask

[<RequireQualifiedAccess>]
module Exn =
  let preserveStackTrace =
    lazy typeof<exn>.GetMethod(
      "InternalPreserveStackTrace",
      BindingFlags.Instance ||| BindingFlags.NonPublic)
    
  let inline reraise exn =
    (exn, null)
      |> preserveStackTrace.Value.Invoke
      |> ignore

    raise exn
