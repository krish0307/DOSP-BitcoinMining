#r "nuget: Akka, 1.4.25"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#load "Util.fs"
#time "on"

open System
open Akka.Actor
open Akka.FSharp
open Akka.Configuration
open Akka.Remote
open System.Diagnostics
open Util


let config =
    Configuration.parse
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                hostname = localhost
                port = 7777
            }
        }"

//Skeleton of Msg
type Message =
    | Start of (int)
    | Connect of (string)
    | WorkInfo of ( int * int)
    | Found of (string*string)
    | Finished of (string)

type RemoteMessaging =
    | StartMining of (int*int)

let actorSystem = System.create "ServerMiner" config

let Worker (mailbox: Actor<_>) =
    let rec loop () =
        actor {
            let! (message: Message) = mailbox.Receive()
            let sender = mailbox.Sender()

            match message with
            | WorkInfo ( workUnit, k) ->
                for i in 0 .. (workUnit) do
                    let input = Util.prefix + Util.randomString(10) + i.ToString()
                    let hashVal = input |> Util.computeSha256Hash
                    if prefixZeroes (hashVal, k) then
                        sender <! Found(hashVal,input);
                sender <! Finished("finished")
                return! loop ()
            | _ -> ()
        }
    loop ()


let ServerMaster (mailbox: Actor<_>) =
    let numberOfWorkers =
        System.Environment.ProcessorCount |> int 
    let workerActorsPool =
        [ 1 .. numberOfWorkers ]
        |> List.map (fun id -> spawn actorSystem (sprintf "Worker(%d)" id) Worker)

    let workerenum = [| for lp in workerActorsPool -> lp |]

    let workerSystem =
        actorSystem.ActorOf(Props.Empty.WithRouter(Akka.Routing.RoundRobinGroup(workerenum)))
    let workUnit=1000000
    let mutable numberOfZeroes=0
    let mutable numOfIterations=0
    let maxIterations=numberOfWorkers
    let rec loop () =
        actor {
            let! (data: obj) = mailbox.Receive()
            let sender = mailbox.Sender()
            
            match data with
            | :? Message as msg ->
                match msg with
                | Start (zeroCount)->
                    numberOfZeroes<-zeroCount
                    for i in 0 .. numberOfWorkers do
                        workerSystem <! WorkInfo(workUnit,zeroCount)
                        
                | Connect ("connect") ->
                    sender <! StartMining(workUnit,numberOfZeroes)
                | Found(hashVal,input)->
                     printf "HashValue %s for Input String %s\n" hashVal input
                | Finished(s) -> 
                    if numOfIterations>=maxIterations then
                        sender <! "shutdown"
                        mailbox.Context.System.Terminate() |> ignore
                    workerSystem <! WorkInfo(workUnit,numberOfZeroes)
                    numOfIterations<-numOfIterations+1
                | _ ->()
            | :? Tuple<string,string> as tuple ->
                    let (hashValue, input): Tuple<string, string> = downcast data
                    printf "HashValue %s and input %s\n"  hashValue input
                    ()
            | :? string as msg ->
                match msg with
                | "connect" ->
                        printf "New Worker Joined\n"
                        sender <! (workUnit,numberOfZeroes)
                | "taskFinished" ->
                        if(numOfIterations=maxIterations) then
                            sender <! "shutdown"
                            mailbox.Context.System.Terminate() |> ignore
                        sender <! (workUnit,numberOfZeroes)
                        numOfIterations<-numOfIterations+1
               
                | _ ->()
            | _ ->() 
            return! loop ()  
        }
    loop ()


let serverMasterRef=spawn actorSystem "ServerMaster" ServerMaster
let numberOfZeroes=(int fsi.CommandLineArgs.[1])

serverMasterRef <! Start(numberOfZeroes)
actorSystem.WhenTerminated.Wait()

// Console.ReadLine() |> ignore
