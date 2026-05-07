// API Endpoints
const API = {
    BankA: 'https://localhost:7298',
    BankB: 'https://localhost:7016',
    Coordinator: 'https://localhost:7041'
};

// DOM Elements
const els = {
    bankABalance: document.getElementById('bankA-balance'),
    bankBBalance: document.getElementById('bankB-balance'),
    formTransfer: document.getElementById('transfer-form'),
    btnTransfer: document.getElementById('btn-transfer'),
    btnSimulate: document.getElementById('btn-simulate-failure'),
    amount: document.getElementById('transfer-amount'),
    consoleOutput: document.getElementById('console-output'),
    btnClearLogs: document.getElementById('btn-clear-logs'),
    selectFrom: document.getElementById('from-account'),
    selectTo: document.getElementById('to-account'),
    labelAId: document.getElementById('bankA-id'),
    labelBId: document.getElementById('bankB-id')
};

// Format Currency
const formatMoney = (amount) => {
    return new Intl.NumberFormat('en-US').format(amount);
};

// Add Log Entry to Console
const log = (message, type = 'info') => {
    const entry = document.createElement('div');
    entry.className = `log-entry ${type}`;
    
    const now = new Date();
    const timeString = `${now.getHours().toString().padStart(2, '0')}:${now.getMinutes().toString().padStart(2, '0')}:${now.getSeconds().toString().padStart(2, '0')}`;
    
    entry.innerHTML = `<span class="timestamp">[${timeString}]</span> ${message}`;
    els.consoleOutput.appendChild(entry);
    
    // Auto scroll to bottom
    els.consoleOutput.scrollTop = els.consoleOutput.scrollHeight;
};

// Fetch Balance
const fetchBalance = async (bank) => {
    try {
        const accountId = bank === 'A' ? els.selectFrom.value : els.selectTo.value;
        const baseUrl = bank === 'A' ? API.BankA : API.BankB;
        
        log(`Fetching balance for ${bank} (${accountId})...`, 'info');
        
        const response = await fetch(`${baseUrl}/Bank/Balance/${accountId}`);
        const data = await response.json();
        
        if (response.ok && data.success) {
            const balanceEl = bank === 'A' ? els.bankABalance : els.bankBBalance;
            balanceEl.innerText = formatMoney(data.data.balance);
            log(`Bank ${bank} Balance: $${formatMoney(data.data.balance)}`, 'success');
        } else {
            throw new Error(data.message || 'Failed to fetch balance');
        }
    } catch (error) {
        log(`Error fetching Bank ${bank} balance: ${error.message}`, 'error');
        const balanceEl = bank === 'A' ? els.bankABalance : els.bankBBalance;
        balanceEl.innerText = 'ERR';
    }
};

// Fetch Both Balances
const fetchAllBalances = () => {
    fetchBalance('A');
    fetchBalance('B');
};

// Handle Transfer
const executeTransfer = async (simulateFailure = false) => {
    const amountStr = els.amount.value;
    const amount = parseFloat(amountStr);
    
    if (isNaN(amount) || amount <= 0) {
        log('Invalid transfer amount.', 'error');
        return;
    }

    const endpoint = simulateFailure ? '/Transfer/simulate-failure' : '/Transfer';
    const logType = simulateFailure ? 'warning' : 'info';
    
    log(`----------------------------------------`, 'info');
    log(`Initiating Transfer: $${amount} from ${els.selectFrom.value} to ${els.selectTo.value} ${simulateFailure ? '(SIMULATE FAILURE)' : ''}`, logType);
    
    // Disable buttons
    els.btnTransfer.disabled = true;
    els.btnSimulate.disabled = true;

    try {
        const response = await fetch(`${API.Coordinator}${endpoint}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                fromAccountId: els.selectFrom.value,
                toAccountId: els.selectTo.value,
                amount: amount,
                simulateFailure: simulateFailure
            })
        });

        const data = await response.json();
        
        if (response.ok) {
            log(`Transfer Status: ${data.status}`, data.status === 'Success' ? 'success' : 'warning');
            log(`Message: ${data.message}`, 'info');
        } else {
            throw new Error(data.message || 'Transfer failed');
        }
    } catch (error) {
        log(`Transfer Error: ${error.message}`, 'error');
    } finally {
        // Re-enable buttons
        els.btnTransfer.disabled = false;
        els.btnSimulate.disabled = false;
        
        // Refresh balances after a short delay
        setTimeout(fetchAllBalances, 1000);
    }
};

// Polling Recovery Logs
let lastRecoveryCount = 0;
const pollRecoveryLogs = async () => {
    try {
        const response = await fetch(`${API.Coordinator}/Transfer/recovery-logs`);
        if (!response.ok) return;
        
        const data = await response.json();
        
        if (data.totalRecoveries > lastRecoveryCount) {
            // New recovery detected!
            log(`!!! RECOVERY DETECTED !!!`, 'recovery');
            log(data.message, 'recovery');
            
            // Print the latest logs
            const newLogs = data.logs.slice(lastRecoveryCount);
            newLogs.forEach(l => {
                log(`[Recovery] Transaction: ${l.transactionId} | Status: ${l.recoveryStatus}`, 'warning');
            });
            
            lastRecoveryCount = data.totalRecoveries;
            
            // Automatically refresh balances because recovery might have refunded Bank A
            setTimeout(fetchAllBalances, 1000);
        }
    } catch (error) {
        // Silently ignore connection errors for polling
    }
};

// Clear Recovery Logs
const clearLogs = async () => {
    try {
        await fetch(`${API.Coordinator}/Transfer/recovery-logs`, { method: 'DELETE' });
        lastRecoveryCount = 0;
        els.consoleOutput.innerHTML = '';
        log('Local console and server recovery logs cleared.', 'success');
    } catch (error) {
        els.consoleOutput.innerHTML = '';
        log('Local console cleared.', 'info');
    }
};

// Event Listeners
els.formTransfer.addEventListener('submit', (e) => {
    e.preventDefault();
    executeTransfer(false);
});

els.btnSimulate.addEventListener('click', () => {
    executeTransfer(true);
});

els.btnClearLogs.addEventListener('click', clearLogs);

els.selectFrom.addEventListener('change', () => {
    els.labelAId.innerText = els.selectFrom.value;
    fetchBalance('A');
});

els.selectTo.addEventListener('change', () => {
    els.labelBId.innerText = els.selectTo.value;
    fetchBalance('B');
});

// Initialization
document.addEventListener('DOMContentLoaded', () => {
    log('Frontend WebDemo loaded successfully.', 'success');
    fetchAllBalances();
    
    // Start polling recovery logs every 5 seconds
    setInterval(pollRecoveryLogs, 5000);
});
