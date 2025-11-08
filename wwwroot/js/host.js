let connection;
let gameId;
let categories = [];
let clueAnswered = {};

document.addEventListener('DOMContentLoaded', function() {
    initializeSignalR();
    
    document.getElementById('createGameBtn').addEventListener('click', createGame);
    document.getElementById('markCorrect').addEventListener('click', () => judgeAnswer(true));
    document.getElementById('markIncorrect').addEventListener('click', () => judgeAnswer(false));
    document.getElementById('closeClueBtn').addEventListener('click', closeClue);
});

function initializeSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/gameHub")
        .build();

    connection.on("GameCreated", (gameCode, gameCategories) => {
        gameId = gameCode;
        categories = gameCategories;
        clueAnswered = {};
        
        document.getElementById('gameCode').textContent = gameCode;
        document.getElementById('gameSelection').classList.add('d-none');
        document.getElementById('gameLobby').classList.remove('d-none');
        document.getElementById('gameBoard').classList.remove('d-none');
        
        buildBoard();
    });

    connection.on("PlayerJoined", (players) => {
        updatePlayersList(players);
    });

    connection.on("ClueSelected", (question, categoryName, value) => {
        showClue(question, categoryName, value);
    });

    connection.on("PlayerBuzzedIn", (playerName, connectionId) => {
        showBuzzedIn(playerName);
    });

    connection.on("AnswerSubmitted", (playerName, answer) => {
        showAnswer(playerName, answer);
    });

    connection.on("AnswerJudged", (isCorrect, players, clueKey) => {
        clueAnswered[clueKey] = true;
        updatePlayersList(players);
        updateBoard();
        
        if (isCorrect) {
            document.getElementById('buzzedInArea').classList.add('d-none');
            setTimeout(() => {
                closeClue();
            }, 3000);
        }
    });

    connection.on("ShowAnswer", (answer) => {
        // Could show the answer to the host
        console.log("Correct answer:", answer);
    });

    connection.on("Error", (message) => {
        alert("Error: " + message);
    });

    connection.start()
        .then(() => {
            console.log("SignalR Connected");
        })
        .catch(err => {
            console.error("SignalR Connection Error: ", err);
        });
}

function createGame() {
    const template = document.getElementById('gameTemplate').value;
    if (!template) {
        alert('Please select a game template');
        return;
    }
    
    connection.invoke("CreateGame", "Jeopardy Game", template);
}

function buildBoard() {
    const categoryRow = document.getElementById('categoryRow');
    const cluesBody = document.getElementById('cluesBody');
    
    categoryRow.innerHTML = '';
    cluesBody.innerHTML = '';
    
    // Add category headers
    categories.forEach(category => {
        const th = document.createElement('th');
        th.className = 'text-white p-3';
        th.style.cssText = 'background-color: #060CE9; font-weight: bold; font-size: 1.2em;';
        th.textContent = category.name || category.Name;
        categoryRow.appendChild(th);
    });
    
    // Get maximum number of clues
    const maxClues = Math.max(...categories.map(cat => {
        const clues = cat.clues || cat.Clues || [];
        return clues.length;
    }));
    
    // Add clue cells
    for (let i = 0; i < maxClues; i++) {
        const tr = document.createElement('tr');
        categories.forEach((category, catIndex) => {
            const td = document.createElement('td');
            td.className = 'p-4';
            td.style.cssText = 'background-color: #060CE9; color: #FFD700; font-weight: bold; font-size: 1.5em; cursor: pointer; min-width: 150px; min-height: 100px;';
            
            const clues = category.clues || category.Clues || [];
            if (clues[i]) {
                const clue = clues[i];
                const clueKey = `${catIndex}-${i}`;
                const isAnswered = clueAnswered[clueKey] || false;
                const clueValue = clue.value || clue.Value;
                const categoryName = category.name || category.Name;
                
                if (isAnswered) {
                    td.textContent = '';
                    td.style.cursor = 'not-allowed';
                    td.style.opacity = '0.5';
                } else {
                    td.textContent = `$${clueValue}`;
                    td.addEventListener('click', () => selectClue(categoryName, clueValue, catIndex, i));
                }
            }
            
            tr.appendChild(td);
        });
        cluesBody.appendChild(tr);
    }
}

function updateBoard() {
    buildBoard();
}

function selectClue(categoryName, value, catIndex, clueIndex) {
    const clueKey = `${catIndex}-${clueIndex}`;
    if (clueAnswered[clueKey]) {
        return; // Already answered
    }
    
    connection.invoke("SelectClue", gameId, categoryName, value);
}

function showClue(question, categoryName, value) {
    document.getElementById('currentQuestion').textContent = question;
    document.getElementById('currentCategory').textContent = categoryName;
    document.getElementById('currentValue').textContent = `$${value}`;
    document.getElementById('currentClueArea').classList.remove('d-none');
    document.getElementById('buzzedInArea').classList.add('d-none');
}

function showBuzzedIn(playerName) {
    document.getElementById('buzzedPlayerName').textContent = `${playerName} buzzed in!`;
    document.getElementById('playerAnswer').textContent = 'Waiting for answer...';
    document.getElementById('buzzedInArea').classList.remove('d-none');
}

function showAnswer(playerName, answer) {
    document.getElementById('playerAnswer').textContent = `Answer: ${answer}`;
}

function judgeAnswer(isCorrect) {
    connection.invoke("JudgeAnswer", gameId, isCorrect);
}

function closeClue() {
    document.getElementById('currentClueArea').classList.add('d-none');
    document.getElementById('buzzedInArea').classList.add('d-none');
    connection.invoke("ResetClue", gameId);
}

function updatePlayersList(players) {
    const playersList = document.getElementById('playersList');
    playersList.innerHTML = '';
    
    players.forEach(player => {
        const li = document.createElement('div');
        li.className = 'list-group-item d-flex justify-content-between align-items-center';
        const playerName = player.name || player.Name || 'Unknown';
        const playerScore = player.score || player.Score || 0;
        li.innerHTML = `
            <span>${playerName}</span>
            <span class="badge bg-primary rounded-pill">$${playerScore}</span>
        `;
        playersList.appendChild(li);
    });
}

