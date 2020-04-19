open System
open System.Text.RegularExpressions
open Grpc.Net.Client
open PolyCoder
open PolyCoder.KVStore.Abstractions
open PolyCoder.KVStore.Client

module ReplUtils =
    let emptyCommand name input =
        if input = name then Some () else None

    let oneParamCommand name input =
        let regex = sprintf "^%s\s+(?<param1>.+)$" name
        let m = Regex.Match(input, regex)
        if m.Success then
            let param1 = m.Groups.["param1"].Value
            Some param1
        else None

    let twoParamCommand name input =
        let regex = sprintf "^%s\s+(?<param1>[^\s]+)\s+(?<param2>.+)$" name
        let m = Regex.Match(input, regex)
        if m.Success then
            let param1 = m.Groups.["param1"].Value
            let param2 = m.Groups.["param2"].Value
            Some(param1, param2)
        else None


open ReplUtils
open PolyCoder.KVStore.Protobuf
open System.Diagnostics

type ReplState =
    | Disconnected
    | Connected of ConnectedInfo
        
and ConnectedInfo = {
    kvstore: KeyValueStore<byte[], byte[]>
    connectionString: string
    disconnect: (unit -> unit) }

let printHelp state =
    Console.ForegroundColor <- ConsoleColor.DarkMagenta
    printfn "Commands:"
    printfn "> status: Returns connection status"
    match state with
    | Disconnected ->
        printfn "> conn <connection-string>: Connect this client to a KVStore server"
    | Connected _ ->
        printfn "> disc: Disconnect this client"
        printfn "> get <key>: Get value associated to given <key>"
        printfn "> set <key> <value>: Set <value> associated to given <key>"
        printfn "> del <key>: Removes given <key> and associated value"
    printfn "> exit: Exit the client"
    printfn "> help: Print help"

let printStopwatch (stopwatch: Stopwatch) =
    Console.ForegroundColor <- ConsoleColor.DarkMagenta
    printfn "Elapsed time: %.6f ms" (stopwatch.Elapsed.TotalMilliseconds)

let printStatus state =
    Console.ForegroundColor <- ConsoleColor.DarkMagenta
    printf "Client is currently "
    match state with
    | Disconnected ->
        Console.ForegroundColor <- ConsoleColor.Red
        printfn "DISCONNECTED!"

    | Connected data ->
        Console.ForegroundColor <- ConsoleColor.Green
        printf "CONNECTED"
        Console.ForegroundColor <- ConsoleColor.DarkMagenta
        printfn " to %s" data.connectionString

let connect connectionString = async {
    let channel = GrpcChannel.ForAddress(connectionString: string)
    let client = KeyValueStoreService.KeyValueStoreServiceClient(channel)
    let wrapper = createKeyValueStore client
    return Connected {
              connectionString = connectionString
              kvstore = wrapper
              disconnect = fun () -> channel.Dispose() }
}

let getValue data key = async {
    let bytes = System.stringToUtf8 key
    let stopwatch = Stopwatch.StartNew()
    match! data.kvstore.retrieve bytes with
    | Some valueBytes ->
        stopwatch.Stop()
        try
            let value = System.utf8ToString valueBytes
            Console.ForegroundColor <- ConsoleColor.Green
            printfn "%s" value
            printStopwatch stopwatch
        with
        | _ ->
            Console.ForegroundColor <- ConsoleColor.DarkGreen
            printfn "%s" (BitConverter.ToString(valueBytes))
    | None ->
        stopwatch.Stop()
        Console.ForegroundColor <- ConsoleColor.Yellow
        printfn "No value found for given key"
        printStopwatch stopwatch
}

let setValue data key value = async {
    let keyBytes = System.stringToUtf8 key
    let valueBytes = System.stringToUtf8 value
    let stopwatch = Stopwatch.StartNew()
    do! data.kvstore.store keyBytes valueBytes
    stopwatch.Stop()
    Console.ForegroundColor <- ConsoleColor.Green
    printfn "key/value stored!"
    printStopwatch stopwatch
}

let deleteValue data key = async {
    let bytes = System.stringToUtf8 key
    let stopwatch = Stopwatch.StartNew()
    do! data.kvstore.remove bytes
    stopwatch.Stop()
    Console.ForegroundColor <- ConsoleColor.Green
    printfn "key deleted!"
    printStopwatch stopwatch
}

let (|ExitCommand|_|) = emptyCommand "exit"
let (|GeneralHelpCommand|_|) = emptyCommand "help"
let (|StatusCommand|_|) = emptyCommand "status"
let (|ConnectCommand|_|) = oneParamCommand "conn"
let (|DisconnectCommand|_|) = emptyCommand "disc"
let (|GetCommand|_|) = oneParamCommand "get"
let (|DeleteCommand|_|) = oneParamCommand "del"
let (|SetCommand|_|) = twoParamCommand "set"

let startRepl () =
    let rec loop state = async {
        match state with
        | Disconnected ->
            Console.ForegroundColor <- ConsoleColor.Gray
        | _ ->
            Console.ForegroundColor <- ConsoleColor.Yellow
        Console.Write("> ")

        Console.ForegroundColor <- ConsoleColor.DarkYellow
        let command = Console.ReadLine()

        Console.ForegroundColor <- ConsoleColor.Gray

        try
            match state, command with
            | Disconnected, ExitCommand ->
                return ()

            | Connected data, ExitCommand ->
                do data.disconnect()
                return ()

            | _, GeneralHelpCommand ->
                printHelp state
                return! loop state

            | _, StatusCommand ->
                printStatus state
                return! loop state

            | Disconnected, ConnectCommand connString ->
                let! state' = connect connString
                return! loop state'

            | Connected data, ConnectCommand connString ->
                do data.disconnect()
                let! state' = connect connString
                return! loop state'

            | Connected data, DisconnectCommand ->
                do data.disconnect()
                return! loop Disconnected

            | Connected data, GetCommand key ->
                do! getValue data key
                return! loop state

            | Connected data, SetCommand(key, value) ->
                do! setValue data key value
                return! loop state

            | Connected data, DeleteCommand key ->
                do! deleteValue data key
                return! loop state

            | _ ->
                Console.ForegroundColor <- ConsoleColor.Red
                printfn "This command in unknown or unavailable in current state. Type \"help\" for instructions"
                return! loop state

        with
        | exn ->
            Console.ForegroundColor <- ConsoleColor.Red
            printfn "Error executing command!"
            Console.ForegroundColor <- ConsoleColor.DarkRed
            printfn "%O" exn
            return! loop state
    }

    loop Disconnected

[<EntryPoint>]
let main argv =
    printfn "Type \"help\" for instructions"
    startRepl() |> Async.RunSynchronously
    0
