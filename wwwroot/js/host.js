let connection;
let gameId;
let categories = [];
let clueAnswered = {};

let customCategories = null;
let isAuthenticated = false;

document.addEventListener('DOMContentLoaded', function() {
    initializeSignalR();
    
    // Check authentication
    checkAuth();
    
    // Load saved games if authenticated
    loadSavedGames();
    
    // Handle CSV file upload
    document.getElementById('csvFile').addEventListener('change', handleCsvUpload);
    
    document.getElementById('createGameBtn').addEventListener('click', createGame);
    document.getElementById('startGameBtn').addEventListener('click', startGame);
    document.getElementById('markCorrect').addEventListener('click', () => judgeAnswer(true));
    document.getElementById('markIncorrect').addEventListener('click', () => judgeAnswer(false));
    // document.getElementById('closeClueBtn').addEventListener('click', closeClue);
    document.getElementById('savedGameSelect').addEventListener('change', handleSavedGameSelect);
    document.getElementById('correctGuesserBehavior').addEventListener('change', handleCorrectGuesserChange);
});

function initializeSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/gameHub")
        .build();

    connection.on("GameCreated", (gameCode, gameCategories) => { console.log("GameCreated", gameCode, gameCategories);
        gameId = gameCode;
        categories = gameCategories;
        clueAnswered = {};
        
        document.getElementById('gameCode').textContent = gameCode;
        document.getElementById('viewLink').href = `/View?gameId=${gameCode}`;
        document.getElementById('gameSelection').classList.add('d-none');
        document.getElementById('gameLobby').classList.remove('d-none');
    });

    connection.on("GameStarted", (playerName, playersInRound, currentRound) => { console.log("GameStarted", playerName, playersInRound, currentRound);
        document.getElementById('gameLobby').classList.add('d-none');
        document.getElementById('gameBoard').classList.remove('d-none');
    })

    connection.on("PlayerJoined", (players) => { console.log("PlayerJoined", players);
        updatePlayersList(players);
    });

    connection.on("ClueSelected", (question, categoryName, value) => { console.log("ClueSelected", question, categoryName, value);
        showClue(question, categoryName, value);
    });

    connection.on("ShowClueCorrectAnswer", (answer) => {
        document.getElementById('correctAnswerText').textContent = answer;
    })

    connection.on("PlayerBuzzedIn", (playerName, connectionId) => { console.log("PlayerBuzzedIn", playerName, connectionId);
        showBuzzedIn(playerName);
    });

    connection.on("AnswerSubmitted", (playerName, answer) => { console.log("AnswerSubmitted", playerName, answer);
        showAnswer(playerName, answer);
    });

    connection.on("AnswerJudged", (isCorrect, players, clueKey) => { console.log("AnswerJudged", isCorrect, players, clueKey);
        document.getElementById('buzzedInArea').classList.add('d-none');
        if (clueKey != null) {
            clueAnswered[clueKey] = true;
            updatePlayersList(players);
        }
    });

    connection.on("ShowCorrectAnswer", (answer) => { console.log("ShowCorrectAnswer", answer);
        // Show correct answer to host when judging
        document.getElementById('correctAnswerText').textContent = answer;
        document.getElementById('correctAnswerArea').classList.remove('d-none');
    });

    connection.on("ShowAnswer", (answer) => { console.log("ShowAnswer", answer);
        // Show answer to all players when correct
        console.log("Correct answer:", answer);
    });

    connection.on("Error", (message) => { console.log("Error", message);
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

function checkAuth() {
    fetch('/api/auth/check')
        .then(response => response.json())
        .then(data => {
            isAuthenticated = data.isAuthenticated;
            if (isAuthenticated) {
                document.getElementById('savedGamesTabItem').style.display = 'block';
            }
        });
}

function loadSavedGames() {
    fetch('/api/games/saved')
        .then(response => {
            if (response.ok) {
                return response.json();
            }
            return [];
        })
        .then(games => {
            const select = document.getElementById('savedGameSelect');
            select.innerHTML = '<option value="">-- Select a saved game --</option>';
            games.forEach(game => {
                const option = document.createElement('option');
                option.value = game.id;
                option.textContent = game.name;
                select.appendChild(option);
            });
        })
        .catch(err => {
            console.error('Error loading saved games:', err);
        });
}

function handleSavedGameSelect() {
    const gameId = document.getElementById('savedGameSelect').value;
    if (!gameId) {
        customCategories = null;
        return;
    }
    
    fetch(`/api/games/saved/${gameId}`)
        .then(response => response.json())
        .then(data => {
            customCategories = data.categories;
        })
        .catch(err => {
            console.error('Error loading saved game:', err);
            document.getElementById('savedGamesError').textContent = 'Error loading game';
            document.getElementById('savedGamesError').classList.remove('d-none');
        });
}

function handleCsvUpload(event) {
    const file = event.target.files[0];
    if (!file) return;
    
    const reader = new FileReader();
    reader.onload = function(e) {
        const csvContent = e.target.result;
        
        fetch('/api/games/import-csv', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ csvContent: csvContent })
        })
        .then(response => response.json())
        .then(data => {
            if (data.categories) {
                customCategories = data.categories;
                document.getElementById('csvError').classList.add('d-none');
            } else {
                throw new Error(data.error || 'Error importing CSV');
            }
        })
        .catch(err => {
            console.error('Error importing CSV:', err);
            document.getElementById('csvError').textContent = err.message || 'Error importing CSV';
            document.getElementById('csvError').classList.remove('d-none');
            customCategories = null;
        });
    };
    reader.readAsText(file);
}

// Options for who selects clues depends on what happens to correct guessers
function handleCorrectGuesserChange(event) {
    switch (event.target.value) {
        case "0":
            document.getElementById("correctGuesserChoosesDiv").classList.remove("d-none");
            break;
    
        default:
            document.getElementById("correctGuesserChoosesDiv").classList.add("d-none");
            break;
    }
}

function createGame() {
    const activeTab = document.querySelector('.nav-link.active').id;
    let selectedCategories = null;
    
    if (activeTab === 'template-tab') {
        const template = document.getElementById('gameTemplate').value;
        if (!template) {
            alert('Please select a game template');
            return;
        }
        // Will use template name in GameHub
    } else if (activeTab === 'saved-tab') {
        if (!customCategories) {
            alert('Please select a saved game');
            return;
        }
        selectedCategories = customCategories;
    } else if (activeTab === 'csv-tab') {
        if (!customCategories) {
            alert('Please upload a CSV file');
            return;
        }
        selectedCategories = customCategories;
    }
    
    const maxPlayersPerRoundInput = document.getElementById('maxPlayersPerRound').value;
    const maxPlayersPerRound = maxPlayersPerRoundInput ? parseInt(maxPlayersPerRoundInput) : null;
    
    const maxPlayersPerGameInput = document.getElementById('maxPlayersPerGame').value;
    const maxPlayersPerGame = maxPlayersPerGameInput ? parseInt(maxPlayersPerGameInput) : null;
    
    // Validate constraints
    if (maxPlayersPerGame !== null && maxPlayersPerRound !== null && maxPlayersPerRound > maxPlayersPerGame) {
        alert('Max players per round cannot exceed max players per game');
        return;
    }
    
    const correctGuesserBehavior = parseInt(document.getElementById('correctGuesserBehavior').value);
    const correctGuesserChooses = document.getElementById('correctGuesserChooses').checked;
    
    const roundTimeLimitInput = document.getElementById('roundTimeLimit').value;
    const roundTimeLimitSeconds = roundTimeLimitInput ? parseInt(roundTimeLimitInput) : null;

    const answerTimeLimitInput = document.getElementById('answerTimeLimit').value;
    const answerTimeLimitSeconds = answerTimeLimitInput ? parseInt(answerTimeLimitInput) : null;
    
    if (selectedCategories) {
        // Use custom categories
        connection.invoke("CreateGameWithCategories", "Jeopardy Game", selectedCategories, maxPlayersPerRound, maxPlayersPerGame, correctGuesserBehavior, correctGuesserChooses, roundTimeLimitSeconds, answerTimeLimitSeconds);
    } else {
        // Use template
        const template = document.getElementById('gameTemplate').value;
        connection.invoke("CreateGame", "Jeopardy Game", template, maxPlayersPerRound, maxPlayersPerGame, correctGuesserBehavior, correctGuesserChooses, roundTimeLimitSeconds, answerTimeLimitSeconds);
    }
}

function startGame() {
    connection.invoke("StartGame", gameId);
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
    document.getElementById('playerAnswer').textContent = 'Waiting for answer...';
    document.getElementById('buzzedInArea').classList.remove('d-none');
}

function showAnswer(playerName, answer) {
    document.getElementById('playerAnswer').textContent = `Answer: ${answer}`;
}

function judgeAnswer(isCorrect) {
    connection.invoke("JudgeAnswer", gameId, isCorrect);
}

function updatePlayersList(players) {
    const playersList = document.getElementById('playersList');
    const lobbyPlayers = document.getElementById('lobbyPlayers');
    const lobbyNoPlayers = document.getElementById('lobbyNoPlayers');

    // Update in-game players list (shown after game starts)
    if (playersList) {
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

    // Update lobby players (clickable boxes before game start)
    if (lobbyPlayers && lobbyNoPlayers) {
        lobbyPlayers.innerHTML = '';

        if (!players || players.length === 0) {
            lobbyNoPlayers.classList.remove('d-none');
            return;
        }

        lobbyNoPlayers.classList.add('d-none');

        players.forEach(player => {
            const playerName = player.name || player.Name || 'Unknown';
            const playerScore = player.score || player.Score || 0;
            const connectionId = player.connectionId || player.ConnectionId || '';

            const card = document.createElement('div');
            card.className = 'card shadow-sm';
            card.style.width = '180px';
            card.style.cursor = 'pointer';
            card.dataset.connectionId = connectionId;

            card.innerHTML = `
                <div class="card-body text-center">
                    <h5 class="card-title mb-2">${playerName}</h5>
                    <p class="card-text mb-1"><small class="text-muted">Score: $${playerScore}</small></p>
                    <p class="card-text"><small class="text-muted">Click to remove</small></p>
                </div>
            `;

            card.addEventListener('click', () => {
                if (!connectionId) {
                    return;
                }

                const confirmRemove = window.confirm(`Remove "${playerName}" from this game?`);
                if (confirmRemove) {
                    connection.invoke("RemovePlayer", gameId, connectionId)
                        .catch(err => {
                            console.error("Failed to remove player:", err);
                        });
                }
            });

            lobbyPlayers.appendChild(card);
        });
    }
}

