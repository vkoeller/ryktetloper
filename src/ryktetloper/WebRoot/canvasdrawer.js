var gameId = -1;

var container = document.getElementById("container")
function clearContainer() {
    container.innerHTML = "";
}

var paths = [];
var playerName = null;
var playerColor = "black";
var playerId = null;
var currentRoundType = null;
var currentRound = null;
 

function drawing(round) {
    clearContainer();
    paths = [];
    let nedtell = document.createElement("div");
    nedtell.id = "countdown";
    nedtell.innerText = "Gjør deg klar!"
    container.appendChild(nedtell); 
    let h1 = document.createElement("h2");
    h1.innerText = "Du skal tegne:"
    container.appendChild(h1);
    let word = document.createElement("div");
    word.innerText = round.prevPage.guess;
    container.appendChild(word);
    let submitContainer = document.createElement("div");
    let submitBtn = document.createElement("button");
    submitBtn.onclick = (e) => submitRound();
    submitBtn.innerText = "ferdig!"
    submitContainer.appendChild(submitBtn);
    container.appendChild(submitContainer);
    let canvas = document.createElement("canvas");
    canvas.classList.add("drawcanvas");
    canvas.setAttribute("width", "800px");
    canvas.setAttribute("height", "600px");
    container.appendChild(canvas);
    

    // canvas.style.position = 'fixed';

    // get canvas 2D context and set him correct size
    let ctx = canvas.getContext('2d');

    var currentPath = [];
    let currentI = 0;
    let drawing = false;
       
    canvas.addEventListener('mousemove', mouseMove);
    canvas.addEventListener('touchmove', touchMove, {passive:false});
    canvas.addEventListener('mousedown', mouseStart);
    canvas.addEventListener('mouseenter', mouseStart);
    canvas.addEventListener('touchstart', touchStart, {passive:false});
    canvas.addEventListener('mouseout', endPath);
    canvas.addEventListener('mouseup', endPath);
    canvas.addEventListener('touchend', (e) => { endPath(e); e.preventDefault(); }, {passive:false});
    canvas.addEventListener('touchcancel', (e) => { endPath(e); e.preventDefault(); }, {passive:false});

// new position from mouse event

    function mouseStart(e) {
        if (e.buttons !== 1) {
            return;
        }
        let pos = getPosition(e);
        startPath(pos);
    }

    function touchStart(e) {
        let pos = getPosition(e.touches[0]);
        startPath(pos);
        e.preventDefault();
    }
    function startPath(pos) {
        drawing = true;
        currentPath = [{start: pos, stop: pos}]
        currentI = 0;
    }

    function mouseMove(e) {
        let currentPos = getPosition(e);
        move(currentPos);
    }
    function touchMove(e) {
        let currentPos = getPosition(e.touches[0]);
        move(currentPos);
        e.preventDefault();
    }
    function move(currentPos) {
        if (drawing) {
            let prevPost = currentPath[currentI];

            ctx.beginPath();

            ctx.lineWidth = 5;
            ctx.lineCap = 'round';
            ctx.strokeStyle = playerColor;

            ctx.moveTo(prevPost.stop.x, prevPost.stop.y);
            ctx.lineTo(currentPos.x, currentPos.y);
            ctx.stroke();

            
            currentPath.push({
                    start: prevPost.stop,
                    stop: currentPos
                });
            currentI++;
        }
    }

    function endPath(e) {
        drawing = false;
        paths.push(currentPath);
    }

    function getPosition(e) {
        let boundingRect = canvas.getBoundingClientRect();
        return {
            x: e.clientX - boundingRect.left,
            y: e.clientY - boundingRect.top
        }
    }
}

function guessing(round) {
    clearContainer();
    let nedtell = document.createElement("div");
    nedtell.id = "countdown";
    nedtell.innerText = "Gjør deg klar!"
    container.appendChild(nedtell);
    let h1 = document.createElement("h2");
    h1.innerText = "Du skal gjette!"
    container.appendChild(h1);
    
    let canvas = document.createElement("canvas");
    canvas.classList.add("drawcanvas");
    canvas.setAttribute("width", "800px");
    canvas.setAttribute("height", "600px");
    container.appendChild(canvas);

    let ctx = canvas.getContext('2d');
    
    let drawing = round.prevPage.drawing;
    
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
    

    let guessCont = document.createElement("div");
    let guessLabel = document.createElement("label")
    guessLabel.innerText = "Hva forestiller tegningen?"
    let guessInput = document.createElement("input");
    guessInput.setAttribute("type", "text");
    guessInput.setAttribute("id", "guessinput");
    guessLabel.appendChild(guessInput);
    guessCont.appendChild(guessLabel);
    container.appendChild(guessCont);
    
    let submitBtn = document.createElement("button");
    submitBtn.onclick = (e) => submitRound();
    submitBtn.innerText = "Gjett!"
    container.appendChild(submitBtn);
    
    //TODO canvas med tegnign
    
    
}

function handlePlayer(e) {
    let body = JSON.parse(e.data);
    playerColor = body.color;
    playerId = body.id;
    playerName = body.name;
    clearContainer();
    
    let h2 = document.createElement("h2");
    h2.innerText = `Hei, ${playerName}`;
    container.appendChild(h2);
    let d = document.createElement("div");
    d.innerText = "Spillet starter straks";
    container.appendChild(d);
    document.body.classList.add("darkbg");
    document.body.style.backgroundColor = playerColor;
}

function handleCountdownTick(e) {
    let body = JSON.parse(e.data);
    let countdown = document.getElementById("countdown"); //TODO legg til dette elemenetet
    if (countdown) {
        countdown.innerText = body.remaining;
    }
}

function handleNextRound(e) {
    let body = JSON.parse(e.data);
    document.body.style.backgroundColor = body.noteboookColor;
    currentRound = body;
    switch (body.prevPage.pageType) {
        case "guess":
            currentRoundType = "drawing";
            drawing(body);
            break;
        case "drawing":
            currentRoundType = "guess";
            guessing(body);
            break;
    }
}

function postRound() {
    let body = {
        notebookId: currentRound.notebookId,
        roundNum: currentRound.currentRoundNum,
        page: {
            playerFilling: playerId,
            pageType: currentRoundType
        }
    }
    switch (currentRoundType) {
        case "guess":
            body.page.guess = document.getElementById("guessinput").value;  //TODO legg til dette elementet
            break;
        case "drawing":
            body.page.drawing = {
                color: playerColor,
                lines: paths
            }
            break;
    }
    currentRound = null;
    return fetch(`/${gameId}/submitround`, {
        method: "POST",
        body: JSON.stringify(body)
    })
}
 
function submitRound() {
    if (currentRound) {
        postRound()
            .then((r) => {
                clearContainer();
                let h2 = document.createElement("h2");
                h2.innerText = "Flott!"
                container.appendChild(h2);
            });
    }
}

function timeoutSubmitRound() {
    if (currentRound) {
        postRound()
            .then((r) => {
                clearContainer();
                let h2 = document.createElement("h2");
                h2.innerText = "Tiden er ute!"
                container.appendChild(h2);
                let d = document.createElement("div");
                d.innerText = "Det du tegnet/skrevet er sendt inn.";
                container.appendChild(d);
            });
    }
}


function handleEndGame(finishedGame) {
    clearContainer();       
    console.log(finishedGame);

    let d = document.createElement("h2");
    d.innerText = "Spillet er slutt";
    container.appendChild(d);
    
}

function connectToGame(gId, name) {
    gameId = gId
    let eventSource = new EventSource(`/play/${gId}?player_name=${name}`);
    eventSource.addEventListener("player", handlePlayer);
    eventSource.addEventListener("nextround", handleNextRound);
    eventSource.addEventListener("endround", (e) => timeoutSubmitRound());
    eventSource.addEventListener("endgame", (e) => { handleEndGame(JSON.parse(e.data)); eventSource.close() });
    eventSource.addEventListener("countdown", handleCountdownTick);
    
}

let gameIdCont = document.createElement("div");
let gameIdLabel = document.createElement("label")
gameIdLabel.innerText = "GameID:"
let gameIdInput = document.createElement("input");
gameIdInput.setAttribute("type", "text");
gameIdLabel.appendChild(gameIdInput);
gameIdCont.appendChild(gameIdLabel);
container.appendChild(gameIdCont);

let nameCont = document.createElement("div");
let nameLabel = document.createElement("label")
nameLabel.innerText = "Navn:"
let nameInput = document.createElement("input");
nameInput.setAttribute("type", "text");
nameLabel.appendChild(nameInput);
nameCont.appendChild(nameLabel);
container.appendChild(nameCont);

let joinBtn = document.createElement("button");
joinBtn.onclick = e => connectToGame(gameIdInput.value, nameInput.value);
joinBtn.innerText = "Bli med!";
container.appendChild(joinBtn);
//TODO timer
