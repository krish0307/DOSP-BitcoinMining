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


let serverPort = 7777

let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                deployment {
                    /remoteecho {
                        remote = ""akka.tcp://RemoteFSharp@localhost:9001""
                    }
                }
            }
            remote {
                helios.tcp {
                    port = 8888
                    hostname = localhost
                }
            }
        }")

//Skeleton of Msg
type Message =
    | Start of (int)
    | Connect of (string)
    | WorkInfo of ( int * int)
    | Found of (string*string)
    | Finished of (string)

type RemoteMessaging =
    | StartMining of (int*int)

let actorSystem =
    ActorSystem.Create("clientSystem", configuration)

let ClientWorker (mailbox: Actor<_>) =
    let rec loop () =
        actor {
            let! (message: Message) = mailbox.Receive()
            let sender = mailbox.Sender()

            match message with
            | WorkInfo ( workUnit, numOfZeroes) ->
                for i in 0 .. workUnit do
                    let input = Util.prefix + Util.randomString(15) + i.ToString()
                    let hashVal = input |> Util.computeSha256Hash
                    if prefixZeroes (hashVal, numOfZeroes) then
                        sender <! Found(hashVal,input);
                sender <! Finished("finished")
                return! loop ()
            | _ -> ()
        }
    loop ()


let RemoteMaster(ipAddress: string) (mailbox: Actor<_>) =
    let numberOfWorkers =
        System.Environment.ProcessorCount |> int
    
    let remoteServer =
        $"akka.tcp://ServerMiner@{ipAddress}:{serverPort}/user/ServerMaster"
    let serverRef = select remoteServer actorSystem
    let workerActorsPool =
        [ 1 .. numberOfWorkers ]
        |> List.map (fun id -> spawn actorSystem (sprintf "ClientWorker(%d)" id) ClientWorker)

    let workerenum = [| for lp in workerActorsPool -> lp |]

    let workerSystem =
        actorSystem.ActorOf(Props.Empty.WithRouter(Akka.Routing.RoundRobinGroup(workerenum)))

    let rec loop () =
        actor {
            let! (message: obj) = mailbox.Receive()
            match message with
            | :? string as s ->
                match s with
                | "shutdown" -> 
                    printf "received shutdown signal\n"
                    serverRef<! "clientShutdown"
                    mailbox.Context.System.Terminate() |> ignore
                | _ -> ()
            | :? Tuple< int, int> as tuple ->
                    let  (workUnit, numberOfZeroes): Tuple< int, int> = downcast message
                    
                    for i in 1 .. numberOfWorkers do
                        workerSystem <! WorkInfo(workUnit,numberOfZeroes)
            | :? Message as msg ->
                match msg with
                    | Connect ("connect")-> 
                        printf "Connect signal is sent to server\n"
                        serverRef <! "connect"
                    | Found (hashVal,input) ->
                        serverRef <! (hashVal,input)
                    | Finished(s) -> 
                        serverRef <! ("taskFinished")
                    | _ -> ( )
            | _ ->()

            return! loop()
        }
    loop()
let serverIpAddress =
    System.Environment.GetCommandLineArgs().[2]

let remoteMasterRef =
        spawn actorSystem "RemoteMaster" (RemoteMaster serverIpAddress)

remoteMasterRef<! Connect("connect")
actorSystem.WhenTerminated.Wait()