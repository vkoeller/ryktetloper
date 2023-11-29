module ryktetloper.Game

open System
open System.Collections.Concurrent
open System.Timers
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open ryktetloper.ClientConnection
open ryktetloper.Drawing

let colors =
    [|
        "coral"
        "brown"
        "cornflowerblue"
        "darkgreen"
        "darkorchid"
        "deeppink"
        "lightslategray"
        "crimson"
        "mediumseagreen"
        "mediumturquoise"
        "mediumaquamarine"
        "darkslateblue"
        "darkgoldenrod"
        "darkmagenta"
        "dimgray"
    |]
   
    
type PageContent =
    | Drawing of Drawing
    | Guess of string
    | Empty

type Page =
    {
        roundNum: int
        playerIdFilling: string
        pageContent: PageContent
    }

type Notebook =
    {
        pages: Page[]
        startWord: string
        belongToPlayerId: string
    }
type Player =
    {
        name: string
        id: string
        color: string
        num: int
        clientConnection: ClientConnection 
    }

type Game =
    {
        gameId : string
        players: Player[]
        notebooks: Notebook[]
        wordsTaken: string[]
        started: bool
        currentRound: int
        adminConnection: ClientConnection
        roundSecRemaining : int
    }

type GameActions =
    | AddPlayer of string * ClientConnection
    | SubmitRound of string * Page
    | NextRound
    | RoundTick
    | RoundEnded
    | StartGame
    | AddAdmin of ClientConnection
    | Stop
    
[<Literal>]
let ROUND_SEC = 60

type GameHandler(logger: ILogger<GameHandler>, applicationLifetime: IHostApplicationLifetime) =
    
    
    let games = ConcurrentDictionary<string, MailboxProcessor<GameActions>>()
    let random = Random.Shared
    
    do applicationLifetime.ApplicationStopping.Register(fun () ->
        for game in games.Values do
            game.Post Stop
        ) |> ignore
    
    let rec sendAllPlayers (state: Game) (event: Send) =
        state.players |> Array.iter (fun p -> p.clientConnection.send event)

    let mapPage page =
        match page.pageContent with
        | Empty -> PageTO.empty page.playerIdFilling
        | Drawing d -> PageTO.drawing page.playerIdFilling d
        | Guess g -> PageTO.guess page.playerIdFilling g
        
    let createGame (game: Game) =
        MailboxProcessor<GameActions>.Start(fun inbox ->
            let roundTimer = new Timer(TimeSpan.FromSeconds 1.)
            roundTimer.Elapsed.AddHandler(fun _ _ -> inbox.Post RoundTick)
            roundTimer.AutoReset <- true
            
            let rec waitingToStart (state:Game) =
                async {
                    match! inbox.Receive() with
                    | AddPlayer (name, connection) -> //remove player
                        logger.LogInformation "Connected player"
                        let pId = Guid.NewGuid().ToString()
                        let pNum = state.players.Length
                        let p = {
                            name = name
                            id = pId
                            color = colors[pNum % colors.Length]
                            num = pNum
                            clientConnection = connection 
                        }
                        logger.LogInformation $"P {p}"
                        let n = {
                            pages = [||]
                            startWord = Words.randomWord state.wordsTaken
                            belongToPlayerId = pId 
                        }
                        
                        let playerTO = { name = p.name; id = p.id; color = p.color; num = p.num; word = n.startWord }
                        connection.send <| Player playerTO 
                        state.adminConnection.send <| Player playerTO
                        return! waitingToStart { state with players = Array.append state.players [|p|]
                                                            notebooks = Array.append state.notebooks [|n|]
                                                            wordsTaken = Array.append state.wordsTaken [|n.startWord|] }
                    | AddAdmin connection ->
                        return! waitingToStart { state with adminConnection = connection }
                    | StartGame ->
                        let numRounds = state.notebooks.Length
                        let initNotebook = state.notebooks |> Array.mapi (fun pId n ->
                            { n with pages = Array.init numRounds (fun rId -> { pageContent = Empty; roundNum = rId; playerIdFilling = state.players[(pId+rId) % numRounds].id }) })
                        if (numRounds % 2) = 0 then //Start by drawing in your own notebook if even number of players
                            Array.zip state.players initNotebook
                            |> Array.iter (fun (p, n) ->  p.clientConnection.send <| Round {
                                noteboookColor = p.color
                                notebookId = p.id
                                currentRoundNum = 0
                                prevPage = PageTO.guess p.id n.startWord
                            })
                            let event = Countdown ROUND_SEC
                            sendAllPlayers state event
                            state.adminConnection.send event
                            roundTimer.Start()
                            return! playingRound { state with notebooks = initNotebook
                                                              started = true
                                                              roundSecRemaining = ROUND_SEC } 
                        else //start by "guessing" the correct word in you notebook if uneven number of players and jump to next round
                            initNotebook |> Array.iter (fun n -> n.pages[0] <- {
                                roundNum = 0
                                playerIdFilling = n.belongToPlayerId
                                pageContent = Guess n.startWord
                            })
                            inbox.Post NextRound
                            return! playingRound { state with notebooks = initNotebook
                                                              started = true
                                                              currentRound = state.currentRound }
                    | Stop ->
                        state.adminConnection.CloseConnection()
                        state.players |> Array.iter (fun p -> p.clientConnection.CloseConnection())
                    | _ -> //ignore other messages
                        return! waitingToStart state
                    
                }
                
            and playingRound (state: Game) =
                async {
                    match! inbox.Receive() with
                    | SubmitRound (notebookId, page) ->
                        if page.roundNum = state.currentRound then
                            let n = state.notebooks |> Array.find (fun n -> n.belongToPlayerId = notebookId)
                            n.pages[page.roundNum] <- page
                            let allSubmitted = state.notebooks |> Array.forall (fun n -> match n.pages[page.roundNum].pageContent with | Empty -> false | _ -> true)
                            if allSubmitted then
                                return! playingRound { state with roundSecRemaining = 0 }
                        return! playingRound state
                    | RoundTick ->
                        if 0 < state.roundSecRemaining then
                            let remaining = state.roundSecRemaining - 1 
                            let event = Countdown remaining
                            sendAllPlayers state event
                            state.adminConnection.send event
                            return! playingRound { state with roundSecRemaining = remaining }
                        else
                            roundTimer.Stop()
                            inbox.Post RoundEnded
                            return! playingRound { state with roundSecRemaining = -1 }
                    | RoundEnded ->
                        logger.LogInformation "round ended"
                        let event = EndRound state.gameId
                        sendAllPlayers state event
                        state.adminConnection.send event
                        
                        let gameEnded = state.currentRound = (state.players.Length - 1)
                        if gameEnded then
                            return! gameFinished state
                        else
                            return! roundFinished state
                    | Stop ->
                        state.adminConnection.CloseConnection()
                        state.players |> Array.iter (fun p -> p.clientConnection.CloseConnection())
                    | _ ->
                        return! playingRound state        
                }

            and roundFinished (state: Game) =
                async {
                    match! inbox.Receive() with
                    | NextRound ->
                        let nextRoundNum = state.currentRound + 1
                        state.players |> Array.iter (fun p ->
                            let numNotebooks = state.notebooks.Length
                            let nextRoundNotebook = state.notebooks[(p.num + nextRoundNum) % numNotebooks]
                            let nextRoundNotebookPlayer = Array.find (fun i -> i.id = nextRoundNotebook.belongToPlayerId) state.players
                            let prevRoundPage = nextRoundNotebook.pages[state.currentRound]
                            let nextRoundTo =
                                {
                                    noteboookColor = nextRoundNotebookPlayer.color
                                    notebookId = nextRoundNotebook.belongToPlayerId
                                    currentRoundNum = nextRoundNum
                                    prevPage = mapPage prevRoundPage
                                }
                            p.clientConnection.send <| Round nextRoundTo
                        )
                        let event = Countdown ROUND_SEC
                        sendAllPlayers state event
                        state.adminConnection.send event
                        roundTimer.Start()
                        return! playingRound { state with currentRound = nextRoundNum
                                                          started = true 
                                                          roundSecRemaining = ROUND_SEC }                     
                    | Stop ->
                        state.adminConnection.CloseConnection()
                        state.players |> Array.iter (fun p -> p.clientConnection.CloseConnection())
                    | _ -> //ignore other messages 
                        return! roundFinished state
                }
                
            and gameFinished (state: Game) =
                async {
                    let finsishedNotebooks = state.notebooks |> Array.map (fun n ->
                        let player = state.players |> Array.find(fun p -> p.id = n.belongToPlayerId)
                        {
                          startWord = n.startWord
                          playerId = n.belongToPlayerId
                          playerName = player.name
                          color = player.color
                          pages = n.pages |> Array.map mapPage
                        })
                    
                    let foldNotebook (n: Notebook) (chainOk:bool, prev:Page option, players:string list) (page:Page): (bool * Page option * string list) =
                        match chainOk, prev, page.pageContent with
                        | true, None, _ -> (true, Some page, players)
                        | true, Some pp, (Guess g) when (g.ToLower()) = (n.startWord.ToLower()) -> (true, Some page, pp.playerIdFilling :: page.playerIdFilling :: players)
                        | true, Some pp, (Guess g) -> (false, Some page, players)
                        | true, Some _, p -> (true, Some page, players)
                        | false, _, p -> (false, Some page, players)
                    
                    let foldAllNotebooks (scores: Map<string,int>) (n: Notebook): Map<string,int> =
                        let (_,_,notebookResult) = Array.fold (foldNotebook n) (true, None, []) n.pages
                        notebookResult
                        |> List.distinct
                        |> List.fold (fun m r -> m |> Map.add r ((Map.find r scores) + 1)) scores
                    
                    let playerScores: PlayerScore[] =
                        state.notebooks
                        |> Array.fold foldAllNotebooks (Map<string, int>(state.players |> Array.map (fun p -> p.id, 0)))
                        |> Map.toArray
                        |> Array.map (fun (pId, score) ->
                            let player = state.players |> Array.find (fun p -> p.id = pId)
                            {
                                playerId = player.id
                                playerColor = player.color
                                playerName = player.name
                                playerScore = score 
                            })
                        |> Array.sortBy (fun s -> s.playerScore)
                        |> Array.rev

                    
                    let event = EndGame { finishedNotebooks = finsishedNotebooks; playerScores = playerScores }
                    sendAllPlayers state event
                    state.adminConnection.send event
                    
                    state.adminConnection.CloseConnection()
                    state.players |> Array.iter (fun p -> p.clientConnection.CloseConnection())
                    return () //End
                }
            
            waitingToStart game)
        
    let getGame gameId =
        match games.TryGetValue gameId with
        | false, _ -> failwith $"no game with id {gameId}"
        | true, gameProcessor -> gameProcessor
        
    member _.NewGame () =
        let game =
            {
                gameId = random.NextInt64(1000000) |> string
                players = [||]
                notebooks = [||]
                wordsTaken = [||]
                started = false
                currentRound = 0
                adminConnection = Unchecked.defaultof<ClientConnection>
                roundSecRemaining = -1 
            }
        let gameProcessor = createGame game
        let s = games.TryAdd(game.gameId, gameProcessor)
        if not s then
            failwith "Failed to create game"
        game.gameId
        
             
    member _.AddPlayer gameId playerName connection =
        let gameProcessor = getGame gameId
        logger.LogInformation "addming player"
        gameProcessor.Post <| AddPlayer (playerName, connection)
    
    member _.SubmitRound gameId notebookId page =
        let gameProcessor = getGame gameId
        gameProcessor.Post <| SubmitRound (notebookId, page)

    member _.StartGame gameId =
        let gameProcessor = getGame gameId
        gameProcessor.Post <| StartGame
        
    member _.NextRound gameId =
        let gameProcessor = getGame gameId
        gameProcessor.Post <| NextRound
        
    member _.AddAdmin gameId connection =
        let gameProcessor = getGame gameId
        gameProcessor.Post <| AddAdmin connection

