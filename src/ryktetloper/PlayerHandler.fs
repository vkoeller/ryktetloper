module ryktetloper.PlayerHandler

open System
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open ryktetloper.ClientConnection
open Game
open Giraffe

type RoundTO =
    {
        page: PageTO
        notebookId: string
        roundNum: int
    }


let handleSubmitRound gameId =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let gameHandler = ctx.GetService<GameHandler>()
            let! input = ctx.BindJsonAsync<RoundTO>()
            let page = {
                roundNum = input.roundNum
                playerIdFilling = input.page.playerFilling
                pageContent =
                    match input.page.pageType with
                    | "guess" -> Guess input.page.guess
                    | "drawing" -> Drawing input.page.drawing
                    | "empty" -> Empty
                    | t -> failwith $"invalid pageType {t}"
            }        
            gameHandler.SubmitRound gameId input.notebookId page
            return! json {| Result = "OK" |} next ctx
        }
    

let handlePlay gameId =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            ctx.Response.ContentType <- "text/event-stream" 
            let gameHandler = ctx.GetService<GameHandler>()
            let logger = ctx.GetLogger()
            let playerName = ctx.TryGetQueryStringValue("player_name") |> Option.defaultValue $"player-{Random.Shared.NextInt64(50)}"
            logger.LogInformation $"connected player name: {playerName}"
            use conn = new ClientConnection(logger, ctx.Response, ctx.RequestAborted)
            
            gameHandler.AddPlayer gameId playerName conn
            
            let _ = ctx.RequestAborted.WaitHandle.WaitOne()
            
            //TODO handle disconnect
            return! next ctx
        }
