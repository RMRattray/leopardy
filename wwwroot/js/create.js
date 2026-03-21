let categories = [];
let categoryCounter = 0;
let activeCategoryId = null;
let editingGameId = null;

document.addEventListener('DOMContentLoaded', function() {
    document.getElementById('addCategoryBtn').addEventListener('click', addCategory);
    const downloadBtn = document.getElementById('downloadCsvBtn');
    if (downloadBtn) {
        downloadBtn.addEventListener('click', downloadCsv);
    }
    document.getElementById('saveGameBtn').addEventListener('click', saveGame);
    loadGameForEditing();
});

function addCategory() {
    const categoryId = `category-${categoryCounter++}`;
    const category = {
        id: categoryId,
        name: '',
        clues: []
    };
    categories.push(category);
    
    addCategoryTab(categoryId, category.name);

    const container = document.getElementById('categoriesContainer');
    const categoryDiv = document.createElement('div');
    categoryDiv.className = 'card mb-3 d-none';
    categoryDiv.id = categoryId;
    categoryDiv.innerHTML = `
        <div class="card-header d-flex justify-content-between align-items-center">
            <div class="flex-grow-1">
                <input type="text" class="form-control category-name" placeholder="Category Name" data-category-id="${categoryId}">
            </div>
            <button class="btn btn-sm btn-danger ms-2 remove-category" data-category-id="${categoryId}">Remove</button>
        </div>
        <div class="card-body">
            <button class="btn btn-sm btn-primary add-clue" data-category-id="${categoryId}">Add Clue</button>
            <div class="clues-container mt-3" data-category-id="${categoryId}"></div>
        </div>
    `;
    
    container.appendChild(categoryDiv);
    
    // Add event listeners
    categoryDiv.querySelector('.category-name').addEventListener('input', function() {
        category.name = this.value;
        updateCategoryTabTitle(categoryId, category.name);
    });
    
    categoryDiv.querySelector('.remove-category').addEventListener('click', function() {
        const catId = this.getAttribute('data-category-id');
        removeCategory(catId);
    });
    
    categoryDiv.querySelector('.add-clue').addEventListener('click', function() {
        const catId = this.getAttribute('data-category-id');
        addClue(catId);
    });

    setActiveCategory(categoryId);
}

function removeCategory(categoryId) {
    categories = categories.filter(c => c.id !== categoryId);
    const tab = document.querySelector(`button[data-category-id="${categoryId}"]`);
    if (tab) {
        tab.closest('li')?.remove();
    }

    const categoryDiv = document.getElementById(categoryId);
    if (categoryDiv) {
        categoryDiv.remove();
    }

    if (activeCategoryId === categoryId) {
        const firstRemainingCategory = categories[0];
        if (firstRemainingCategory) {
            setActiveCategory(firstRemainingCategory.id);
        } else {
            activeCategoryId = null;
        }
    }
}

function addClue(categoryId) {
    const category = categories.find(c => c.id === categoryId);
    if (!category) return;
    
    const clueId = `clue-${Date.now()}-${Math.random()}`;
    const clue = {
        id: clueId,
        question: '',
        answer: '',
        value: (category.clues.length + 1) * 200
    };
    category.clues.push(clue);
    
    const container = document.querySelector(`.clues-container[data-category-id="${categoryId}"]`);
    const clueDiv = document.createElement('div');
    clueDiv.className = 'card mb-2';
    clueDiv.id = clueId;
    clueDiv.innerHTML = `
        <div class="card-body">
            <div class="row">
                <div class="col-md-1">
                    <label class="form-label">Value</label>
                    <input type="number" class="form-control clue-value" value="${clue.value}" data-clue-id="${clueId}">
                </div>
                <div class="col-md-5">
                    <label class="form-label">Question</label>
                    <input type="text" class="form-control clue-question" placeholder="Enter question" data-clue-id="${clueId}">
                </div>
                <div class="col-md-5">
                    <label class="form-label">Answer</label>
                    <input type="text" class="form-control clue-answer" placeholder="Enter answer" data-clue-id="${clueId}">
                </div>
                <div class="col-md-1">
                    <label class="form-label">&nbsp;</label>
                    <button class="btn btn-sm btn-danger remove-clue w-100" data-clue-id="${clueId}">X</button>
                </div>
            </div>
        </div>
    `;
    
    container.appendChild(clueDiv);
    
    // Add event listeners
    clueDiv.querySelector('.clue-value').addEventListener('input', function() {
        clue.value = parseInt(this.value) || 0;
    });
    
    clueDiv.querySelector('.clue-question').addEventListener('input', function() {
        clue.question = this.value;
    });
    
    clueDiv.querySelector('.clue-answer').addEventListener('input', function() {
        clue.answer = this.value;
    });
    
    clueDiv.querySelector('.remove-clue').addEventListener('click', function() {
        const clId = this.getAttribute('data-clue-id');
        removeClue(categoryId, clId);
    });
}

function removeClue(categoryId, clueId) {
    const category = categories.find(c => c.id === categoryId);
    if (category) {
        category.clues = category.clues.filter(c => c.id !== clueId);
    }
    const clueDiv = document.getElementById(clueId);
    if (clueDiv) {
        clueDiv.remove();
    }
}

function downloadCsv() {
    const gameData = buildGameData();
    if (!gameData || gameData.categories.length === 0) {
        alert('Please add at least one category with clues');
        return;
    }
    
    fetch('/api/games/export-csv', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(gameData)
    })
    .then(response => response.text())
    .then(csv => {
        const blob = new Blob([csv], { type: 'text/csv' });
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        const gameName = document.getElementById('gameName').value || 'jeopardy-game';
        a.download = `${gameName.replace(/[^a-z0-9]/gi, '_').toLowerCase()}.csv`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
    })
    .catch(err => {
        console.error('Error downloading CSV:', err);
        alert('Error downloading CSV');
    });
}

function saveGame() {
    const gameName = document.getElementById('gameName').value.trim();
    if (!gameName) {
        alert('Please enter a game name');
        return;
    }
    
    const gameData = buildGameData();
    if (!gameData || gameData.categories.length === 0) {
        alert('Please add at least one category with clues');
        return;
    }
    
    fetch('/api/games/save', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            id: editingGameId,
            name: gameName,
            categories: gameData.categories
        })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            window.location.href = `/Games?savedGameId=${data.id}`;
        } else {
            alert('Error saving game: ' + (data.error || 'Unknown error'));
        }
    })
    .catch(err => {
        console.error('Error saving game:', err);
        alert('Error saving game');
    });
}

function buildGameData() {
    const gameName = document.getElementById('gameName').value.trim();
    
    const gameCategories = categories
        .filter(c => c.name.trim() !== '')
        .map(c => ({
            name: c.name.trim(),
            clues: c.clues
                .filter(cl => cl.question.trim() !== '' && cl.answer.trim() !== '')
                .map(cl => ({
                    question: cl.question.trim(),
                    answer: cl.answer.trim(),
                    value: cl.value || 200
                }))
        }))
        .filter(c => c.clues.length > 0);
    
    return {
        name: gameName,
        categories: gameCategories
    };
}

function addCategoryTab(categoryId, categoryName) {
    const tabs = document.getElementById('categoryTabs');
    const item = document.createElement('li');
    item.className = 'nav-item';
    item.role = 'presentation';

    const button = document.createElement('button');
    button.className = 'nav-link';
    button.type = 'button';
    button.setAttribute('data-category-id', categoryId);
    button.textContent = categoryName || `Category ${categories.length}`;
    button.addEventListener('click', function() {
        setActiveCategory(categoryId);
    });

    item.appendChild(button);
    tabs.appendChild(item);
}

function updateCategoryTabTitle(categoryId, categoryName) {
    const button = document.querySelector(`button[data-category-id="${categoryId}"]`);
    if (button) {
        const category = categories.find(c => c.id === categoryId);
        const fallbackIndex = category ? categories.indexOf(category) + 1 : 1;
        button.textContent = categoryName?.trim() || `Category ${fallbackIndex}`;
    }
}

function setActiveCategory(categoryId) {
    activeCategoryId = categoryId;

    document.querySelectorAll('#categoryTabs .nav-link').forEach(tab => {
        const isActive = tab.getAttribute('data-category-id') === categoryId;
        tab.classList.toggle('active', isActive);
    });

    document.querySelectorAll('#categoriesContainer > .card').forEach(card => {
        card.classList.toggle('d-none', card.id !== categoryId);
    });
}

function loadGameForEditing() {
    const params = new URLSearchParams(window.location.search);
    const editGameId = params.get('savedGameId');
    if (!editGameId) {
        addCategory();
        return;
    }

    fetch(`/api/games/saved/${editGameId}`)
        .then(response => {
            if (!response.ok) {
                throw new Error('Unable to load saved game');
            }
            return response.json();
        })
        .then(data => {
            editingGameId = data.id;
            document.getElementById('gameName').value = data.name || '';
            categories = [];
            categoryCounter = 0;
            activeCategoryId = null;
            document.getElementById('categoriesContainer').innerHTML = '';
            document.getElementById('categoryTabs').innerHTML = '';

            const savedCategories = Array.isArray(data.categories) ? data.categories : [];
            if (savedCategories.length === 0) {
                addCategory();
                return;
            }

            savedCategories.forEach(savedCategory => {
                addCategory();
                const category = categories[categories.length - 1];
                category.name = savedCategory.name || '';

                const categoryInput = document.querySelector(`#${category.id} .category-name`);
                if (categoryInput) {
                    categoryInput.value = category.name;
                }
                updateCategoryTabTitle(category.id, category.name);

                (savedCategory.clues || []).forEach(savedClue => {
                    addClue(category.id);
                    const clue = category.clues[category.clues.length - 1];
                    clue.value = savedClue.value || 200;
                    clue.question = savedClue.question || '';
                    clue.answer = savedClue.answer || '';

                    const clueDiv = document.getElementById(clue.id);
                    if (!clueDiv) {
                        return;
                    }

                    const valueInput = clueDiv.querySelector('.clue-value');
                    const questionInput = clueDiv.querySelector('.clue-question');
                    const answerInput = clueDiv.querySelector('.clue-answer');
                    if (valueInput) valueInput.value = clue.value;
                    if (questionInput) questionInput.value = clue.question;
                    if (answerInput) answerInput.value = clue.answer;
                });
            });

            if (categories.length > 0) {
                setActiveCategory(categories[0].id);
            }
        })
        .catch(err => {
            console.error('Error loading saved game for editing:', err);
            alert('Unable to load saved game for editing.');
            addCategory();
        });
}

