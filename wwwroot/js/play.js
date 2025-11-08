let connection;
let gameId;
let playerName;
let categories = [];
let clueAnswered = {};
let answerTimer;
let timeRemaining = 5;
let canBuzz = false;
let hasBuzzedIn = false;

document.addEventListener('DOMContentLoaded', function() {
    initializeSignalR();
    
    document.getElementById('joinGameBtn').addEventListener('click', joinGame);
    document.getElementById('buzzBtn').addEventListener('click', buzzIn);
    document.getElementById('submitAnswerBtn').addEventListener('click', submitAnswer);
    
    // Listen for spacebar to buzz in
    document.addEventListener('keydown', function(event) {
        if (event.code === 'Space' && canBuzz && !hasBuzzedIn) {
            event.preventDefault();
            buzzIn();
        }
    });
    
    // Listen for clicks anywhere on clue view to buzz in
    document.getElementById('clueView')?.addEventListener('click', function(event) {
        if (canBuzz && !hasBuzzedIn && event.target.id !== 'answerInput' && event.target.id !== 'submitAnswerBtn') {
            buzzIn();
        }
    });
});

function initializeSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/gameHub")
        .build();

    connection.on("JoinedGame", (gameCategories, answered) => {
        categories = gameCategories;
        clueAnswered = answered || {};
        
        document.getElementById('joinGame').classList.add('d-none');
        document.getElementById('gameView').classList.remove('d-none');
        document.getElementById('playerNameDisplay').textContent = playerName;
        
        buildBoard();
    });

    connection.on("ClueSelected", (question, categoryName, value) => {
        showClue(question, categoryName, value);
        canBuzz = true;
        hasBuzzedIn = false;
        document.getElementById('answerArea').classList.add('d-none');
        document.getElementById('buzzArea').classList.remove('d-none');
    });

    connection.on("PlayerBuzzedIn", (buzzedPlayerName, connectionId) => {
        if (buzzedPlayerName === playerName) {
            // This player buzzed in first
            startAnswerTimer();
            document.getElementById('buzzArea').classList.add('d-none');
            document.getElementById('answerArea').classList.remove('d-none');
            document.getElementById('answerInput').focus();
        } else {
            // Another player buzzed in
            canBuzz = false;
            document.getElementById('buzzBtn').disabled = true;
            document.getElementById('buzzBtn').textContent = `${buzzedPlayerName} buzzed in!`;
        }
    });

    connection.on("AnswerSubmitted", (submittedPlayerName, answer) => {
        // Could show that an answer was submitted
    });

    connection.on("AnswerJudged", (isCorrect, players, clueKey) => {
        clueAnswered[clueKey] = true;
        updateScore(players);
        updateBoard();
        
        // Reset for next clue
        setTimeout(() => {
            hideClue();
            canBuzz = false;
            hasBuzzedIn = false;
        }, 2000);
    });

    connection.on("ClueReset", () => {
        hideClue();
        canBuzz = false;
        hasBuzzedIn = false;
    });

    connection.on("Error", (message) => {
        alert("Error: " + message);
    });

    connection.start()
        .then(() => {
            console.log("SignalR Connected");
            // Connection ID is managed server-side, we don't need it on the client
        })
        .catch(err => {
            console.error("SignalR Connection Error: ", err);
        });
}

function joinGame() {
    playerName = document.getElementById('playerName').value.trim();
    gameId = document.getElementById('gameCodeInput').value.trim();
    
    if (!playerName) {
        alert('Please enter your name');
        return;
    }
    
    if (!gameId) {
        alert('Please enter a game code');
        return;
    }
    
    connection.invoke("JoinGame", gameId, playerName);
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
                
                if (isAnswered) {
                    td.textContent = '';
                    td.style.cursor = 'not-allowed';
                    td.style.opacity = '0.5';
                } else {
                    td.textContent = `$${clueValue}`;
                    // Players can only select clues if they have control (got previous clue correct)
                    // For now, allow all players to see the board
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

function updateScore(players) {
    // Find player by name since we don't have connection ID on client
    const player = players.find(p => (p.name || p.Name) === playerName);
    if (player) {
        const score = player.score || player.Score || 0;
        document.getElementById('playerScore').textContent = score;
    }
}

function showClue(question, categoryName, value) {
    document.getElementById('clueQuestion').textContent = question;
    document.getElementById('clueCategory').textContent = categoryName;
    document.getElementById('clueValue').textContent = `$${value}`;
    document.getElementById('boardView').classList.add('d-none');
    document.getElementById('clueView').classList.remove('d-none');
    document.getElementById('buzzBtn').disabled = false;
    document.getElementById('buzzBtn').textContent = 'Buzz In (Spacebar)';
}

function hideClue() {
    document.getElementById('clueView').classList.add('d-none');
    document.getElementById('boardView').classList.remove('d-none');
    document.getElementById('answerArea').classList.add('d-none');
    document.getElementById('buzzArea').classList.add('d-none');
    if (answerTimer) {
        clearInterval(answerTimer);
    }
}

function buzzIn() {
    if (!canBuzz || hasBuzzedIn) {
        return;
    }
    
    hasBuzzedIn = true;
    canBuzz = false;
    connection.invoke("BuzzIn", gameId);
}

function startAnswerTimer() {
    timeRemaining = 5;
    document.getElementById('timeRemaining').textContent = timeRemaining;
    
    answerTimer = setInterval(() => {
        timeRemaining--;
        document.getElementById('timeRemaining').textContent = timeRemaining;
        const percentage = (timeRemaining / 5) * 100;
        document.getElementById('timerBar').style.width = percentage + '%';
        
        if (timeRemaining <= 0) {
            clearInterval(answerTimer);
            // Auto-submit if time runs out
            submitAnswer();
        }
    }, 1000);
}

function submitAnswer() {
    if (answerTimer) {
        clearInterval(answerTimer);
    }
    
    const answer = document.getElementById('answerInput').value.trim();
    if (!answer) {
        alert('Please enter an answer');
        return;
    }
    
    connection.invoke("SubmitAnswer", gameId, answer);
    document.getElementById('answerInput').value = '';
    document.getElementById('answerArea').classList.add('d-none');
}

