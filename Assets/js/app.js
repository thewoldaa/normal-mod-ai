(function() {
    const chatBox = document.getElementById('chat-box');
    const msgInput = document.getElementById('msg-input');
    const btnSend = document.getElementById('btn-send');
    const typingIndicator = document.getElementById('typing-indicator');
    const btnMic = document.getElementById('btn-mic');
    const btnSticker = document.getElementById('btn-sticker');
    const stickerPanel = document.getElementById('sticker-panel');
    const bgUpload = document.getElementById('bg-upload');
    const btnWallpaper = document.getElementById('btn-wallpaper');
    const btnBgSize = document.getElementById('btn-bg-size');
    const btnClear = document.getElementById('btn-clear');
    const btnTheme = document.getElementById('btn-theme');
    const btnUploadImage = document.getElementById('btn-upload-image');
    const imgUpload = document.getElementById('img-upload');
    const btnSettings = document.getElementById('btn-settings');
    const settingsOverlay = document.getElementById('settings-overlay');
    const btnSettingsClose = document.getElementById('btn-settings-close');
    const btnExport = document.getElementById('btn-export');
    const btnImport = document.getElementById('btn-import');
    const btnClearData = document.getElementById('btn-clear-data');
    const modelPathInput = document.getElementById('model-path');
    const btnSaveModel = document.getElementById('btn-save-model');
    const btnPickModel = document.getElementById('btn-pick-model');

    const storageKeys = {
        chatHistory: 'alya.chatHistory',
        settings: 'alya.settings',
        modelPath: 'alya.modelPath'
    };

    let isRecording = false;

    function scrollToBottom() {
        chatBox.scrollTop = chatBox.scrollHeight;
    }

    function showTypingIndicator() {
        typingIndicator.style.display = 'flex';
        scrollToBottom();
    }

    function hideTypingIndicator() {
        typingIndicator.style.display = 'none';
    }

    function toBase64(str) {
        return btoa(unescape(encodeURIComponent(str)));
    }

    function fromBase64(b64) {
        return decodeURIComponent(escape(atob(b64)));
    }

    function saveSettings(settings) {
        localStorage.setItem(storageKeys.settings, JSON.stringify(settings));
    }

    function loadSettings() {
        const raw = localStorage.getItem(storageKeys.settings);
        if (!raw) {
            return {
                theme: 'dark',
                bgImage: null,
                bgSize: 'cover'
            };
        }
        try {
            return JSON.parse(raw);
        } catch {
            return { theme: 'dark', bgImage: null, bgSize: 'cover' };
        }
    }

    function applySettings(settings) {
        if (settings.theme === 'light') {
            document.body.classList.add('light-mode');
        } else {
            document.body.classList.remove('light-mode');
        }
        if (settings.bgImage) {
            document.body.style.backgroundImage = `url(${settings.bgImage})`;
        }
        document.body.style.backgroundSize = settings.bgSize || 'cover';
        document.body.style.backgroundRepeat = (settings.bgSize === 'cover') ? 'initial' : 'no-repeat';
    }

    function getExportPayload() {
        return {
            version: 1,
            exportedAt: new Date().toISOString(),
            chatHistory: localStorage.getItem(storageKeys.chatHistory) || '[]',
            settings: localStorage.getItem(storageKeys.settings) || '{}',
            modelPath: localStorage.getItem(storageKeys.modelPath) || ''
        };
    }

    function applyImportPayload(payload) {
        if (!payload || typeof payload !== 'object') {
            alert('Data impor tidak valid.');
            return;
        }
        if (payload.chatHistory) {
            localStorage.setItem(storageKeys.chatHistory, payload.chatHistory);
        }
        if (payload.settings) {
            localStorage.setItem(storageKeys.settings, payload.settings);
        }
        if (payload.modelPath !== undefined) {
            localStorage.setItem(storageKeys.modelPath, payload.modelPath);
            if (window.AlyaBridge && window.AlyaBridge.setModelPath) {
                window.AlyaBridge.setModelPath(payload.modelPath);
            }
        }
        chatBox.querySelectorAll('.message-wrapper').forEach(w => w.remove());
        if (window.AlyaBridge && window.AlyaBridge.clearChatHistory) {
            window.AlyaBridge.clearChatHistory();
        }
        loadMessages();
        applySettings(loadSettings());
    }

    window.__applyImportBase64 = function(base64) {
        try {
            const json = fromBase64(base64);
            const payload = JSON.parse(json);
            applyImportPayload(payload);
            alert('Impor selesai.');
        } catch (e) {
            console.error(e);
            alert('Gagal impor data.');
        }
    };

    function saveMessages() {
        const messages = [];
        const wrappers = chatBox.querySelectorAll('.message-wrapper');
        wrappers.forEach(wrapper => {
            const messageDiv = wrapper.querySelector('.message');
            if (!messageDiv) return;
            const sender = messageDiv.classList.contains('user') ? 'user' : 'ai';
            const isSticker = messageDiv.classList.contains('sticker');
            const img = messageDiv.querySelector('img');
            const imgSrc = img ? img.src : null;
            const content = imgSrc ? '' : messageDiv.innerText;
            messages.push({ sender, content, isSticker, imgSrc });
        });
        localStorage.setItem(storageKeys.chatHistory, JSON.stringify(messages));
    }

    function loadMessages() {
        const saved = localStorage.getItem(storageKeys.chatHistory);
        if (saved) {
            try {
                const messages = JSON.parse(saved);
                messages.forEach(msg => {
                    if (msg.imgSrc) {
                        appendImageMessage(msg.imgSrc, msg.sender, true);
                    } else {
                        appendMessage(msg.content, msg.sender, msg.isSticker, true);
                    }
                });
                return;
            } catch (e) {
                console.error('Gagal memuat chat history', e);
            }
        }
        appendMessage('Hai! Aku Alya. Senang bisa ngobrol sama kamu. Ada yang bisa kubantu hari ini?', 'ai', false, true);
    }

    function appendMessage(content, sender, isSticker = false, skipSave = false) {
        const wrapper = document.createElement('div');
        wrapper.classList.add('message-wrapper', sender + '-wrapper');

        const msgDiv = document.createElement('div');
        msgDiv.classList.add('message', sender);
        if (isSticker) msgDiv.classList.add('sticker');
        msgDiv.innerText = content;

        const actions = document.createElement('div');
        actions.classList.add('message-actions');

        const deleteBtn = document.createElement('button');
        deleteBtn.textContent = '🗑️';
        deleteBtn.title = 'Hapus';
        deleteBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            wrapper.remove();
            saveMessages();
        });
        actions.appendChild(deleteBtn);

        if (sender === 'user' && !isSticker) {
            const editBtn = document.createElement('button');
            editBtn.textContent = '✏️';
            editBtn.title = 'Edit';
            editBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                const newText = prompt('Edit pesan:', msgDiv.innerText);
                if (newText !== null && newText.trim() !== '') {
                    msgDiv.innerText = newText.trim();
                    saveMessages();
                }
            });
            actions.appendChild(editBtn);
        }

        if (sender === 'ai' && !isSticker) {
            const ttsBtn = document.createElement('button');
            ttsBtn.textContent = '🔊';
            ttsBtn.title = 'Dengarkan';
            ttsBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                if (window.AlyaBridge && window.AlyaBridge.speak) {
                    window.AlyaBridge.speak(msgDiv.innerText);
                } else if (window.speechSynthesis) {
                    const utterance = new SpeechSynthesisUtterance(msgDiv.innerText);
                    utterance.lang = 'id-ID';
                    window.speechSynthesis.speak(utterance);
                } else {
                    alert('Text-to-speech tidak tersedia.');
                }
            });
            actions.appendChild(ttsBtn);
        }

        wrapper.appendChild(msgDiv);
        wrapper.appendChild(actions);
        chatBox.insertBefore(wrapper, typingIndicator);

        scrollToBottom();
        if (!skipSave) saveMessages();
    }

    function appendImageMessage(src, sender, skipSave = false) {
        const wrapper = document.createElement('div');
        wrapper.classList.add('message-wrapper', sender + '-wrapper');

        const msgDiv = document.createElement('div');
        msgDiv.classList.add('message', sender);
        const img = document.createElement('img');
        img.src = src;
        img.alt = 'Gambar';
        msgDiv.appendChild(img);

        const actions = document.createElement('div');
        actions.classList.add('message-actions');
        const deleteBtn = document.createElement('button');
        deleteBtn.textContent = '🗑️';
        deleteBtn.title = 'Hapus';
        deleteBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            wrapper.remove();
            saveMessages();
        });
        actions.appendChild(deleteBtn);

        wrapper.appendChild(msgDiv);
        wrapper.appendChild(actions);
        chatBox.insertBefore(wrapper, typingIndicator);

        scrollToBottom();
        if (!skipSave) saveMessages();
    }

    function sendMessage() {
        const text = msgInput.value.trim();
        if (!text) return;

        appendMessage(text, 'user', false);
        msgInput.value = '';

        showTypingIndicator();

        if (window.AlyaBridge && window.AlyaBridge.sendMessage) {
            try {
                const reply = window.AlyaBridge.sendMessage(text);
                hideTypingIndicator();
                appendMessage(reply || 'Maaf, Alya sedang menyiapkan jawaban.', 'ai', false);
            } catch (e) {
                console.error(e);
                hideTypingIndicator();
                appendMessage('Maaf, terjadi kesalahan saat memproses pesan.', 'ai', false);
            }
        } else {
            setTimeout(() => {
                hideTypingIndicator();
                const aiResponses = [
                    'Menarik sekali!',
                    'Aku mengerti maksudmu.',
                    'Tentu, aku bisa membantu soal itu.',
                    'Bisa jelaskan lebih detail?',
                    'Sistem offline beroperasi dengan baik.'
                ];
                const randomReply = aiResponses[Math.floor(Math.random() * aiResponses.length)];
                appendMessage(randomReply, 'ai', false);
            }, 900);
        }
    }

    btnSend.addEventListener('click', sendMessage);
    msgInput.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') sendMessage();
    });

    btnSticker.addEventListener('click', () => {
        stickerPanel.classList.toggle('active');
    });

    document.querySelectorAll('.sticker-item').forEach(item => {
        item.addEventListener('click', function() {
            const emoji = this.getAttribute('data-sticker');
            appendMessage(emoji, 'user', true);
            stickerPanel.classList.remove('active');

            showTypingIndicator();
            setTimeout(() => {
                hideTypingIndicator();
                appendMessage('🤖👍', 'ai', true);
            }, 800);
        });
    });

    btnMic.addEventListener('click', () => {
        isRecording = !isRecording;
        if (isRecording) {
            btnMic.classList.add('recording');
            msgInput.placeholder = 'Mendengarkan...';
        } else {
            btnMic.classList.remove('recording');
            msgInput.placeholder = 'Ketik pesan...';
            msgInput.value = 'Ini adalah hasil dari input suara.';
        }
    });

    btnWallpaper.addEventListener('click', () => bgUpload.click());
    bgUpload.addEventListener('change', function() {
        const file = this.files[0];
        if (file) {
            const reader = new FileReader();
            reader.onload = function(e) {
                const settings = loadSettings();
                settings.bgImage = e.target.result;
                saveSettings(settings);
                applySettings(settings);
            };
            reader.readAsDataURL(file);
        }
    });

    btnBgSize.addEventListener('click', () => {
        const settings = loadSettings();
        settings.bgSize = (settings.bgSize === 'cover') ? 'contain' : 'cover';
        saveSettings(settings);
        applySettings(settings);
    });

    btnClear.addEventListener('click', () => {
        if (confirm('Apakah kamu yakin ingin menghapus semua riwayat chat?')) {
            const wrappers = chatBox.querySelectorAll('.message-wrapper');
            wrappers.forEach(w => w.remove());
            if (window.AlyaBridge && window.AlyaBridge.clearChatHistory) {
                window.AlyaBridge.clearChatHistory();
            }
            appendMessage('Hai! Aku Alya. Senang bisa ngobrol sama kamu. Ada yang bisa kubantu hari ini?', 'ai', false);
        }
    });

    btnTheme.addEventListener('click', () => {
        const settings = loadSettings();
        settings.theme = (settings.theme === 'light') ? 'dark' : 'light';
        saveSettings(settings);
        applySettings(settings);
    });

    btnUploadImage.addEventListener('click', () => imgUpload.click());
    imgUpload.addEventListener('change', function() {
        const file = this.files[0];
        if (file) {
            const reader = new FileReader();
            reader.onload = function(e) {
                appendImageMessage(e.target.result, 'user');
                showTypingIndicator();
                setTimeout(() => {
                    hideTypingIndicator();
                    appendMessage('Gambar diterima! Keren!', 'ai', false);
                }, 900);
            };
            reader.readAsDataURL(file);
        }
        this.value = '';
    });

    btnSettings.addEventListener('click', () => {
        settingsOverlay.classList.add('active');
        settingsOverlay.setAttribute('aria-hidden', 'false');
    });
    btnSettingsClose.addEventListener('click', () => {
        settingsOverlay.classList.remove('active');
        settingsOverlay.setAttribute('aria-hidden', 'true');
    });
    settingsOverlay.addEventListener('click', (e) => {
        if (e.target === settingsOverlay) {
            settingsOverlay.classList.remove('active');
            settingsOverlay.setAttribute('aria-hidden', 'true');
        }
    });

    btnExport.addEventListener('click', () => {
        const payload = JSON.stringify(getExportPayload());
        if (window.AlyaBridge && window.AlyaBridge.exportData) {
            window.AlyaBridge.exportData(toBase64(payload));
        } else {
            alert('Ekspor tidak tersedia di mode ini.');
        }
    });

    btnImport.addEventListener('click', () => {
        if (window.AlyaBridge && window.AlyaBridge.requestImport) {
            window.AlyaBridge.requestImport();
        } else {
            alert('Impor tidak tersedia di mode ini.');
        }
    });

    btnClearData.addEventListener('click', () => {
        if (!confirm('Hapus semua data lokal?')) return;
        localStorage.removeItem(storageKeys.chatHistory);
        localStorage.removeItem(storageKeys.settings);
        localStorage.removeItem(storageKeys.modelPath);
        if (window.AlyaBridge && window.AlyaBridge.clearNativeData) {
            window.AlyaBridge.clearNativeData();
        }
        chatBox.querySelectorAll('.message-wrapper').forEach(w => w.remove());
        appendMessage('Hai! Aku Alya. Senang bisa ngobrol sama kamu. Ada yang bisa kubantu hari ini?', 'ai', false);
    });

    btnSaveModel.addEventListener('click', () => {
        const path = modelPathInput.value.trim();
        localStorage.setItem(storageKeys.modelPath, path);
        if (window.AlyaBridge && window.AlyaBridge.setModelPath) {
            window.AlyaBridge.setModelPath(path);
        }
        alert('Path model disimpan.');
    });

    if (btnPickModel) {
        btnPickModel.addEventListener('click', () => {
            if (window.AlyaBridge && window.AlyaBridge.requestModelPick) {
                window.AlyaBridge.requestModelPick();
            } else {
                alert('Pemilih file tidak tersedia di mode ini.');
            }
        });
    }

    chatBox.addEventListener('dblclick', (e) => {
        const target = e.target.closest('.message.user');
        if (target && !target.classList.contains('sticker')) {
            const newText = prompt('Edit pesan:', target.innerText);
            if (newText !== null && newText.trim() !== '') {
                target.innerText = newText.trim();
                saveMessages();
            }
        }
    });

    const settings = loadSettings();
    applySettings(settings);
    loadMessages();

    if (window.AlyaBridge && window.AlyaBridge.getModelPath) {
        try {
            const nativePath = window.AlyaBridge.getModelPath();
            const stored = localStorage.getItem(storageKeys.modelPath) || '';
            modelPathInput.value = nativePath || stored;
        } catch {
            modelPathInput.value = localStorage.getItem(storageKeys.modelPath) || '';
        }
    } else {
        modelPathInput.value = localStorage.getItem(storageKeys.modelPath) || '';
    }
})();
