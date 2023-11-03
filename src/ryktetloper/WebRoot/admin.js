var gameId = -1;

var adminContainer = document.getElementById("admincontainer")
function clearAdminContainer() {
    adminContainer.innerHTML = "";
    
}

function playingRoundView() { //TODO få inn round len her så vi kan begynne telle på det
    let countdown = document.createElement("h1");
    countdown.setAttribute("id", "countdown");
    adminContainer.appendChild(countdown);
    let t = document.createElement("div");
    t.innerText = "Seconds remaining of round";
    adminContainer.appendChild(t);
}


function startGame(e) {
    clearAdminContainer()
    fetch(`/admin/startgame/${gameId}`, {
        method: "POST"
    }).then((r) => {
        return r.json();
    }).then (body => {
        playingRoundView();
    })
}

function nextRound(e) {
    clearAdminContainer()
    fetch(`/admin/nextround/${gameId}`, {
        method: "POST"
    }).then((r) => {
        return r.json();
    }).then (body => {
        playingRoundView();
    })
}

function handleAddPlayer(e) {
    let userDiv = document.createElement("div");
    let player = JSON.parse(e.data);
    userDiv.setAttribute("style", `background-color: ${player.color}`);
    userDiv.classList.add("playerbadge");
    userDiv.innerText = player.name;
    adminContainer.appendChild(userDiv);

}

function handleEndRound(e) {
    let body = JSON.parse(e.data);
    clearAdminContainer();

    let nextRoundBtn = document.createElement("button");
    nextRoundBtn.innerText = "Next Round";
    nextRoundBtn.onclick = nextRound;
    adminContainer.appendChild(nextRoundBtn);
}

function handleCountdownTick(e) {
    let body = JSON.parse(e.data);
    let countdown = document.getElementById("countdown");
    if (countdown) {
        countdown.innerText = body.remaining;
    }
}
function handleEndGame(finishedGame) {
    clearAdminContainer();
    let d = document.createElement("h2");
    d.innerText = "Spillet er slutt";
    adminContainer.appendChild(d);
    
    for (const playerScore of finishedGame.playerScores) {
        let userDiv = document.createElement("div");
        userDiv.setAttribute("style", `background-color: ${playerScore.playerColor}`);
        userDiv.classList.add("playerbadge");
        userDiv.innerText = playerScore.playerScore + " - " + playerScore.playerName;
        adminContainer.appendChild(userDiv);
    }
    
    for (const nb of finishedGame.finishedNotebooks) {
        let art = document.createElement("article");
        
        
        let h3 = document.createElement("h3")
        h3.innerText = nb.playerName;
        art.appendChild(h3);
        
        let riktig = document.createElement("h4");
        riktig.innerText = "Riktig ord: " + nb.startWord;
        art.appendChild(riktig);
        
        for (const pag of nb.pages) {
            let pagePlayer = finishedGame.playerScores.find(p => p.playerId === pag.playerFilling);
            
            let pElm = document.createElement("div");
            pElm.classList.add("resultpage")
            pElm.style.backgroundColor = nb.color;
            
            if (pag.pageType === "guess") {
                let g = document.createElement("h4");
                g.innerText = pagePlayer.playerName + " gjettet: " + pag.guess;
                pElm.appendChild(g);
            } else if (pag.pageType === "drawing") {
                let dh = document.createElement("h4");
                dh.innerText = pagePlayer.playerName + " tegnet:";
                pElm.appendChild(dh);

                let canvas = document.createElement("canvas");
                canvas.classList.add("drawcanvas");
                canvas.setAttribute("width", "800px");
                canvas.setAttribute("height", "600px");
                pElm.appendChild(canvas);

                let ctx = canvas.getContext('2d');

                let drawing = pag.drawing;

                for (const line of drawing.lines) {
                    for (const linePoint of line) {
                        ctx.beginPath();

                        ctx.lineWidth = 5;
                        ctx.lineCap = 'round';
                        ctx.strokeStyle = drawing.color;

                        ctx.moveTo(linePoint.start.x, linePoint.start.y);
                        ctx.lineTo(linePoint.stop.x, linePoint.stop.y);
                        ctx.stroke();
                    }
                }
            }
            
            art.appendChild(pElm);
        } 
        adminContainer.appendChild(art);
    }
    
}

function listenForUpdates(url) {
    let eventSource = new EventSource(url);
    eventSource.addEventListener("player", handleAddPlayer);
    eventSource.addEventListener("endround", handleEndRound);
    eventSource.addEventListener("endgame", (e) => {handleEndGame(JSON.parse(e.data)); eventSource.close() });
    eventSource.addEventListener("countdown", handleCountdownTick);
}
function createGame(e) {
    fetch("/admin/newgame", {
        method: "POST"
    }).then ((r) => {
        return r.json()
    }).then ((b) => {
        gameId = b.gameId;
        clearAdminContainer();
        let gameIdT = document.createElement("h1");
        gameIdT.innerText = b.gameId;
        adminContainer.appendChild(gameIdT)
        let startBtn = document.createElement("button");
        startBtn.innerText = "Start game";
        startBtn.onclick = startGame;
        adminContainer.appendChild(startBtn);
        listenForUpdates(b.link)
    })    
}

let createBtn = document.createElement("button");
createBtn.innerText = "Create game";
createBtn.onclick = createGame;
adminContainer.appendChild(createBtn);

