let connection;
let gameId;
let categories = [];
let clueAnswered = {};
let playersInRound = [];
let waitingPlayers = [];
let currentRound = 1;
let buzzIns = [];
let answers = [];

document.addEventListener('DOMContentLoaded', function() {
    // Get gameId from URL
    const urlParams = new URLSearchParams(window.location.search);
    gameId = urlParams.get('gameId');
    
    if (!gameId) {
        alert('No game ID provided');
        return;
    }
    
    document.getElementById('gameIdDisplay').textContent = gameId;
    
    initializeSignalR();
});

function initializeSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/gameHub")
        .build();

    connection.on("GameStarted", (playerName, playersInCurrentRound, round, playersWaiting) => {
        playersInRound = playersInCurrentRound || [];
        waitingPlayers = playersWaiting || [];
        currentRound = round || 1;
        document.getElementById('waitingScreen').classList.add('d-none');
        document.getElementById('gameView').classList.remove('d-none');
        document.getElementById('currentRound').textContent = currentRound;
        updatePlayersInRound();
        updateWaitingPlayers();
        if (categories.length > 0) {
            buildBoard();
        }
        updateChooser();
    });

    connection.on("PlayerJoined", (players) => {
        // Update players list if needed
    });

    connection.on("RoundStarted", (round, playersInCurrentRound, firstPlayerName, playersWaiting) => {
        playersInRound = playersInCurrentRound || [];
        waitingPlayers = playersWaiting || [];
        currentRound = round || 1;
        document.getElementById('currentRound').textContent = currentRound;
        updatePlayersInRound();
        updateWaitingPlayers();
        updateChooser();
    });

    connection.on("ClueSelected", (question, categoryName, value) => {
        showClue(question, categoryName, value);
        buzzIns = [];
        answers = [];
        updateBuzzIns();
    });

    connection.on("PlayerBuzzedIn", (playerName, connectionId) => {
        if (!buzzIns.find(b => b.name === playerName)) {
            buzzIns.push({ name: playerName, connectionId: connectionId, time: new Date() });
            updateBuzzIns();
        }
    });

    connection.on("AnswerSubmitted", (playerName, answer) => {
        if (!answers.find(a => a.name === playerName)) {
            answers.push({ name: playerName, answer: answer });
            updateAnswers();
        }
    });

    connection.on("AnswerJudged", (isCorrect, players, clueKey, playersInCurrentRound, playersWaiting) => {
        playersInRound = playersInCurrentRound || [];
        waitingPlayers = playersWaiting || [];
        clueAnswered[clueKey] = true;
        updatePlayersInRound();
        updateWaitingPlayers();
        updateBoard();
        updateChooser();
        
        setTimeout(() => {
            hideClue();
        }, 3000);
    });

    connection.on("ClueReset", () => {
        hideClue();
    });

    connection.on("JoinedGame", (gameCategories, answered) => {
        categories = gameCategories || [];
        clueAnswered = answered || {};
        
        // If we have categories, build the board
        if (categories.length > 0) {
            buildBoard();
        }
    });

    connection.on("PlayerJoined", (players) => {
        // Update if needed
    });

    connection.on("Error", (message) => {
        console.error("Error: " + message);
    });

    connection.start()
        .then(() => {
            console.log("SignalR Connected");
            // Join the game group to receive updates
            if (gameId) {
                connection.invoke("JoinGame", gameId, "Viewer").catch(err => {
                    console.error("Failed to join game as viewer:", err);
                });
            }
        })
        .catch(err => {
            console.error("SignalR Connection Error: ", err);
        });
}

function updatePlayersInRound() {
    const playersList = document.getElementById('playersInRound');
    playersList.innerHTML = '';
    
    playersInRound.forEach(player => {
        const li = document.createElement('div');
        li.className = 'list-group-item d-flex justify-content-between align-items-center';
        const playerName = player.name || player.Name || 'Unknown';
        const playerScore = player.score || player.Score || 0;
        const hasControl = player.hasControl || player.HasControl || false;
        
        li.innerHTML = `
            <span>${playerName} ${hasControl ? '<span class="badge bg-success">Choosing</span>' : ''}</span>
            <span class="badge bg-primary rounded-pill">$${playerScore}</span>
        `;
        playersList.appendChild(li);
    });
}

function updateChooser() {
    const chooserArea = document.getElementById('playerChooserArea');
    const chooserName = document.getElementById('chooserPlayerName');
    
    const playerWithControl = playersInRound.find(p => (p.hasControl || p.HasControl));
    
    if (playerWithControl && !document.getElementById('currentClueArea').classList.contains('d-none')) {
        // Clue is active, don't show chooser
        chooserArea.classList.add('d-none');
    } else if (playerWithControl) {
        const playerName = playerWithControl.name || playerWithControl.Name || 'Unknown';
        chooserName.textContent = `${playerName} should choose a clue`;
        chooserArea.classList.remove('d-none');
    } else {
        chooserArea.classList.add('d-none');
    }
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
            td.style.cssText = 'background-color: #060CE9; color: #FFD700; font-weight: bold; font-size: 1.5em; min-width: 150px; min-height: 100px;';
            
            const clues = category.clues || category.Clues || [];
            if (clues[i]) {
                const clue = clues[i];
                const clueKey = `${catIndex}-${i}`;
                const isAnswered = clueAnswered[clueKey] || false;
                const clueValue = clue.value || clue.Value;
                
                if (isAnswered) {
                    td.textContent = '';
                    td.style.opacity = '0.5';
                } else {
                    td.textContent = `$${clueValue}`;
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

function showClue(question, categoryName, value) {
    document.getElementById('currentQuestion').textContent = question;
    document.getElementById('currentCategory').textContent = categoryName;
    document.getElementById('currentValue').textContent = `$${value}`;
    document.getElementById('currentClueArea').classList.remove('d-none');
    document.getElementById('playerChooserArea').classList.add('d-none');
    updateBuzzIns();
}

function hideClue() {
    document.getElementById('currentClueArea').classList.add('d-none');
    updateChooser();
    buzzIns = [];
    answers = [];
    updateBuzzIns();
    updateAnswers();
}

function updateBuzzIns() {
    const buzzInsList = document.getElementById('buzzInsList');
    buzzInsList.innerHTML = '';
    
    if (buzzIns.length === 0) {
        buzzInsList.innerHTML = '<div class="list-group-item">No buzz-ins yet</div>';
    } else {
        buzzIns.forEach((buzz, index) => {
            const li = document.createElement('div');
            li.className = 'list-group-item';
            li.innerHTML = `<strong>${index + 1}.</strong> ${buzz.name}`;
            buzzInsList.appendChild(li);
        });
    }
}

function updateAnswers() {
    const answersList = document.getElementById('answersList');
    const answersArea = document.getElementById('answersArea');
    
    if (answers.length === 0) {
        answersArea.classList.add('d-none');
        return;
    }
    
    answersArea.classList.remove('d-none');
    answersList.innerHTML = '';
    
    answers.forEach(answer => {
        const li = document.createElement('div');
        li.className = 'list-group-item';
        li.innerHTML = `<strong>${answer.name}:</strong> ${answer.answer}`;
        answersList.appendChild(li);
    });
}

function updateWaitingPlayers() {
    const waitingPlayersList = document.getElementById('waitingPlayers');
    if (!waitingPlayersList) return;
    
    waitingPlayersList.innerHTML = '';
    
    if (waitingPlayers.length === 0) {
        waitingPlayersList.innerHTML = '<div class="list-group-item text-muted">No players waiting</div>';
        return;
    }
    
    waitingPlayers.forEach((player, index) => {
        const li = document.createElement('div');
        li.className = 'list-group-item d-flex justify-content-between align-items-center';
        const playerName = player.name || player.Name || 'Unknown';
        const playerScore = player.score || player.Score || 0;
        const isUpNext = index === 0;
        
        li.innerHTML = `
            <span>${isUpNext ? '<span class="badge bg-warning text-dark me-2">Up Next</span>' : ''}${playerName}</span>
            <span class="badge bg-secondary rounded-pill">$${playerScore}</span>
        `;
        waitingPlayersList.appendChild(li);
    });
}

