module ryktetloper.ClientConnection

open System
open System.Text
open System.Threading
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open ryktetloper.Drawing

type PlayerTO =
    {
        name: string
        id: string
        color: string
        num: int
        word: string
    }


type PageTO =
    {
        pageType: string
        playerFilling: string
        guess: string
        drawing: Drawing
    }
    
module PageTO =
    let empty prevPlayerId = { pageType = "empty"; playerFilling = prevPlayerId; drawing = Unchecked.defaultof<Drawing> ; guess = null }
    let drawing prevPlayerId d = { pageType = "drawing"; playerFilling = prevPlayerId; drawing = d ; guess = null }
    
    let guess prevPlayerId g = { pageType = "guess"; playerFilling = prevPlayerId; drawing = Unchecked.defaultof<Drawing> ; guess = g }
    
type NextRound =
    {
        noteboookColor: string
        notebookId: string
        currentRoundNum: int
        prevPage: PageTO
    }
    
    
type PlayerScore =
    {
        playerId: string
        playerName: string
        playerColor: string
        playerScore: int
    }

type FinishedNotebookTO =
    {
        startWord: string
        playerName: string
        playerId: string
        color: string
        pages: PageTO[]
    }

type FinishedGameTO =
    {
        playerScores: PlayerScore[]
        finishedNotebooks: FinishedNotebookTO[]
    }
type Send =
    | Player of PlayerTO
    | Round of NextRound
    | Countdown of int 
    | EndRound of string
    | EndGame of FinishedGameTO
    
type Msg =
    | Send of Send
    | Close
    

let createSseMsg (s: Send) =
    let createEvent name data =
        $"event: {name}\ndata: {Json.serialize data}\n\n"  
    let event =
        match s with
        | Player p ->
            createEvent "player" p
        | Round r ->
            createEvent "nextround" r
        | EndRound gameId ->
            createEvent "endround" {| link = $"/{gameId}/submitround" |}
        | EndGame endState ->
            createEvent "endgame" endState
        | Countdown s ->
            createEvent "countdown" {| remaining = s |}
        
    
    Encoding.UTF8.GetBytes event
    
type ClientConnection(logger: ILogger, response: HttpResponse, token: CancellationToken) =
    
    let mailbox = MailboxProcessor.Start(fun (inbox: MailboxProcessor<Msg>) ->
        let rec loop state =
            async {
                let! msg = inbox.Receive()
                match msg with
                | Send s ->
                    let body = createSseMsg s
                    do! response.Body.WriteAsync(body, 0, body.Length, token) |> Async.AwaitTask
                    return! loop state
                | Close ->
                    response.HttpContext.Abort()
                    return ()
            }
        loop ())        

    member this.send (msg: Send) =
        logger.LogInformation $"sending msg %A{msg}"
        mailbox.Post <| Send msg
        

    member this.CloseConnection () =
        mailbox.Post Close
        
    interface IDisposable with
        member this.Dispose() = (mailbox :> IDisposable).Dispose()
        

