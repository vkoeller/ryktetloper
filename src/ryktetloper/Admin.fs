module ryktetloper.Admin

open Giraffe
open Microsoft.AspNetCore.Http
open ryktetloper.ClientConnection
open ryktetloper.Game
open Microsoft.Extensions.Logging

// ---------------------------------
// Views
// ---------------------------------

module Views =
    open Giraffe.ViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "ryktetloper" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
            ]
            body [] content
        ]

    let index =
        [
            div [ _id "admincontainer" ] []
            script [ _src "/admin.js" ] []
        ] |> layout

// ---------------------------------
// Web app
// ---------------------------------

let indexHandler: HttpHandler =
    htmlView Views.index


let newGameHandler: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let gameHandler = ctx.GetService<GameHandler>()
            let gId = gameHandler.NewGame()
            return! json {| gameId = gId; link = $"/admin/updates/{gId}" |} next ctx
        }
        
let startGameHandler gameId =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let gameHandler = ctx.GetService<GameHandler>()
            gameHandler.StartGame gameId
            return! json {| result = "OK" |} next ctx
        }
        
let nextRoundHandler gameId =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let gameHandler = ctx.GetService<GameHandler>()
            gameHandler.NextRound gameId
            return! json {| result = "OK" |} next ctx
        }        


let handleUpdates gameId =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            ctx.Response.ContentType <- "text/event-stream" 
            let gameHandler = ctx.GetService<GameHandler>()
            let logger = ctx.GetLogger()
            logger.LogInformation "connected admin name"
            use conn = new ClientConnection(logger, ctx.Response, ctx.RequestAborted)
            
            gameHandler.AddAdmin gameId conn
            
            let _ = ctx.RequestAborted.WaitHandle.WaitOne()
            
            //TODO handle disconnect
            return! next ctx
        }
