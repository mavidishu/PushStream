/**
 * PushStream Live Order Tracker Demo
 * A polished, real-world demonstration of real-time order tracking
 */

// ===== DOM Elements =====
const elements = {
    // Connection status
    statusDot: document.getElementById('status-dot'),
    statusText: document.getElementById('status-text'),
    
    // Views
    orderView: document.getElementById('order-view'),
    trackingView: document.getElementById('tracking-view'),
    
    // Order form
    restaurantGrid: document.getElementById('restaurant-grid'),
    menuItems: document.getElementById('menu-items'),
    placeOrderBtn: document.getElementById('place-order-btn'),
    orderTotal: document.getElementById('order-total'),
    
    // Tracking
    trackingOrderId: document.getElementById('tracking-order-id'),
    trackingTitle: document.getElementById('tracking-title'),
    etaTime: document.getElementById('eta-time'),
    timeline: document.getElementById('timeline'),
    driverCard: document.getElementById('driver-card'),
    driverName: document.getElementById('driver-name'),
    driverRating: document.getElementById('driver-rating'),
    driverVehicle: document.getElementById('driver-vehicle'),
    driverPlate: document.getElementById('driver-plate'),
    deliveryProgress: document.getElementById('delivery-progress'),
    orderSummaryItems: document.getElementById('order-summary-items'),
    orderSummaryTotal: document.getElementById('order-summary-total'),
    deliveredCelebration: document.getElementById('delivered-celebration'),
    newOrderBtn: document.getElementById('new-order-btn'),
    
    // Code tabs
    codeTabs: document.querySelectorAll('.code-tab'),
    codeServer: document.getElementById('code-server'),
    codeClient: document.getElementById('code-client')
};

// ===== State =====
const state = {
    selectedRestaurant: 'burger-palace',
    cart: new Map(), // name -> { name, price, quantity }
    currentOrder: null,
    currentStage: null
};

// Stage order for comparison
const stageOrder = ['Confirmed', 'Preparing', 'ReadyForPickup', 'OutForDelivery', 'Delivered'];

// ===== PushStream Client =====
const client = new PushStream.EventClient('/events');

// Connection events
client.on('stream.open', () => {
    elements.statusDot.classList.add('connected');
    elements.statusText.textContent = 'Live';
});

client.on('stream.close', () => {
    elements.statusDot.classList.remove('connected');
    elements.statusText.textContent = 'Reconnecting...';
});

client.on('stream.error', () => {
    elements.statusText.textContent = 'Connection error';
});

// Order update events
client.on('order.updated', (data) => {
    if (state.currentOrder && data.orderId === state.currentOrder.orderId) {
        updateOrderStatus(data);
    }
});

// ===== View Management =====
function showView(viewName) {
    elements.orderView.classList.remove('active');
    elements.trackingView.classList.remove('active');
    
    if (viewName === 'order') {
        elements.orderView.classList.add('active');
    } else {
        elements.trackingView.classList.add('active');
    }
}

// ===== Restaurant Selection =====
elements.restaurantGrid.addEventListener('click', (e) => {
    const card = e.target.closest('.restaurant-card');
    if (!card) return;
    
    document.querySelectorAll('.restaurant-card').forEach(c => c.classList.remove('selected'));
    card.classList.add('selected');
    state.selectedRestaurant = card.dataset.id;
});

// ===== Cart Management =====
elements.menuItems.addEventListener('click', (e) => {
    const btn = e.target.closest('.qty-btn');
    if (!btn) return;
    
    const menuItem = btn.closest('.menu-item');
    const name = menuItem.dataset.name;
    const price = parseFloat(menuItem.dataset.price);
    const qtySpan = menuItem.querySelector('.qty-value');
    
    let item = state.cart.get(name) || { name, price, quantity: 0 };
    
    if (btn.classList.contains('plus')) {
        item.quantity++;
    } else if (btn.classList.contains('minus') && item.quantity > 0) {
        item.quantity--;
    }
    
    if (item.quantity > 0) {
        state.cart.set(name, item);
    } else {
        state.cart.delete(name);
    }
    
    qtySpan.textContent = item.quantity;
    updateCartTotal();
});

function updateCartTotal() {
    let total = 0;
    state.cart.forEach(item => {
        total += item.price * item.quantity;
    });
    
    elements.orderTotal.textContent = `$${total.toFixed(2)}`;
    elements.placeOrderBtn.disabled = total === 0;
}

// ===== Place Order =====
elements.placeOrderBtn.addEventListener('click', async () => {
    if (state.cart.size === 0) return;
    
    elements.placeOrderBtn.disabled = true;
    elements.placeOrderBtn.innerHTML = '<span>Placing order...</span>';
    
    const items = Array.from(state.cart.values()).map(item => ({
        name: item.name,
        quantity: item.quantity,
        price: item.price
    }));
    
    try {
        const response = await fetch('/api/orders/place', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                restaurantId: state.selectedRestaurant,
                items: items,
                deliveryAddress: '123 Main Street'
            })
        });
        
        if (!response.ok) throw new Error('Failed to place order');
        
        const order = await response.json();
        state.currentOrder = order;
        state.currentStage = null;
        
        // Setup tracking view
        setupTrackingView(order, items);
        showView('tracking');
        
    } catch (error) {
        console.error('Error placing order:', error);
        alert('Failed to place order. Please try again.');
    } finally {
        elements.placeOrderBtn.disabled = false;
        elements.placeOrderBtn.innerHTML = '<span>Place Order</span><span id="order-total">$0.00</span>';
        updateCartTotal();
    }
});

// ===== Tracking View Setup =====
function setupTrackingView(order, items) {
    // Reset timeline
    document.querySelectorAll('.timeline-step').forEach(step => {
        step.classList.remove('completed', 'active');
        step.querySelector('.timeline-message').textContent = '';
        step.querySelector('.timeline-time').textContent = '';
    });
    
    // Hide driver card and celebration
    elements.driverCard.classList.remove('visible');
    elements.deliveredCelebration.classList.remove('visible');
    elements.timeline.style.display = 'block';
    
    // Set order info
    elements.trackingOrderId.textContent = `Order ${order.orderId}`;
    elements.trackingTitle.textContent = 'Confirming your order...';
    elements.etaTime.textContent = `${order.estimatedMinutes} min`;
    
    // Render order summary
    let total = 0;
    elements.orderSummaryItems.innerHTML = items.map(item => {
        const itemTotal = item.price * item.quantity;
        total += itemTotal;
        return `
            <div class="order-item">
                <span><span class="order-item-qty">${item.quantity}x</span> ${item.name}</span>
                <span>$${itemTotal.toFixed(2)}</span>
            </div>
        `;
    }).join('');
    elements.orderSummaryTotal.textContent = `$${total.toFixed(2)}`;
}

// ===== Update Order Status =====
function updateOrderStatus(data) {
    const { stage, stageName, message, eta, progress, driver } = data;
    
    state.currentStage = stage;
    
    // Update header
    elements.trackingTitle.textContent = message;
    
    if (eta) {
        const etaDate = new Date(eta);
        const now = new Date();
        const diffMins = Math.max(0, Math.round((etaDate - now) / 60000));
        elements.etaTime.textContent = diffMins > 0 ? `${diffMins} min` : 'Any moment';
    }
    
    // Update timeline
    const stageIndex = stageOrder.indexOf(stage);
    document.querySelectorAll('.timeline-step').forEach((step, index) => {
        const stepStage = step.dataset.stage;
        const stepIndex = stageOrder.indexOf(stepStage);
        
        step.classList.remove('completed', 'active');
        
        if (stepIndex < stageIndex) {
            step.classList.add('completed');
        } else if (stepIndex === stageIndex) {
            step.classList.add('active');
            step.querySelector('.timeline-message').textContent = message;
            step.querySelector('.timeline-time').textContent = formatTime(new Date());
        }
    });
    
    // Show driver card for delivery stages
    if (driver && (stage === 'ReadyForPickup' || stage === 'OutForDelivery')) {
        elements.driverName.textContent = driver.name;
        elements.driverRating.textContent = driver.rating;
        elements.driverVehicle.textContent = `• ${driver.vehicle}`;
        elements.driverPlate.textContent = `• ${driver.plate}`;
        elements.driverCard.classList.add('visible');
        
        // Update delivery progress
        if (progress !== null && progress !== undefined) {
            const deliveryStart = 50; // Progress at which delivery starts
            const deliveryProgress = Math.max(0, Math.min(100, ((progress - deliveryStart) / (100 - deliveryStart)) * 100));
            elements.deliveryProgress.style.width = `${deliveryProgress}%`;
        }
    }
    
    // Handle delivery complete
    if (stage === 'Delivered') {
        setTimeout(() => {
            elements.timeline.style.display = 'none';
            elements.driverCard.classList.remove('visible');
            elements.deliveredCelebration.classList.add('visible');
        }, 500);
    }
}

function formatTime(date) {
    return date.toLocaleTimeString('en-US', { 
        hour: 'numeric', 
        minute: '2-digit',
        hour12: true 
    });
}

// ===== New Order Button =====
elements.newOrderBtn.addEventListener('click', () => {
    // Reset cart
    state.cart.clear();
    state.currentOrder = null;
    state.currentStage = null;
    
    // Reset quantity displays
    document.querySelectorAll('.qty-value').forEach(span => {
        span.textContent = '0';
    });
    
    updateCartTotal();
    showView('order');
});

// ===== Code Tabs =====
elements.codeTabs.forEach(tab => {
    tab.addEventListener('click', () => {
        elements.codeTabs.forEach(t => t.classList.remove('active'));
        tab.classList.add('active');
        
        if (tab.dataset.tab === 'server') {
            elements.codeServer.style.display = 'block';
            elements.codeClient.style.display = 'none';
        } else {
            elements.codeServer.style.display = 'none';
            elements.codeClient.style.display = 'block';
        }
    });
});

// ===== Initialize =====
client.connect();
