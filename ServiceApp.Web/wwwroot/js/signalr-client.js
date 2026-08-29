// =================================================================
//  signalr-client.js
//
//  Connects to the ServiceHub and handles real-time notifications.
//
//  FLOW:
//  1. Page loads → connect to /hubs/service
//  2. On connected → call JoinGroup with role + userId
//  3. Register handlers for each notification event
//  4. When server sends a notification → show toast + update UI
//
//  DATA ATTRIBUTES ON BODY TAG:
//  The layout passes user info to JS via data attributes:
//  <body data-user-id="..." data-user-role="...">
//  JS reads these to join the correct group.
// =================================================================

(function () {
    'use strict';

    // ── Read user info from body data attributes ──────────────────
    var body = document.body;
    var userId = body.dataset.userId;
    var userRole = body.dataset.userRole;

    // Don't connect if user is not logged in
    if (!userId || !userRole) return;

    // ── Build the connection ──────────────────────────────────────
    var connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/service')
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    // ── Register notification handlers ────────────────────────────
    // Each handler matches a SendAsync("EventName") call on the server

    // ── Admin handlers ─────────────────────────────────────────────

    connection.on('NewRequest', function (data) {
        showToast(
            '🔔 New request',
            data.message,
            'info',
            '/Admin/Requests/Index'
        );
        updatePendingBadge(1);
    });

    connection.on('StatusChanged', function (data) {
        showToast(
            '📋 Status updated',
            data.message,
            'info',
            null
        );
        // Refresh admin stat cards if on dashboard
        refreshDashboardCounts();
    });

    // ── Technician handlers ────────────────────────────────────────

    connection.on('JobAssigned', function (data) {
        showToast(
            '🔧 New job assigned!',
            data.message,
            'success',
            '/Technician/Jobs/Index'
        );
        playNotificationSound();
    });

    connection.on('NewJobAvailable', function (data) {
        showToast(
            '⚡ New ' + data.category + ' job',
            data.message,
            'info',
            '/Technician/Jobs/Index'
        );
    });

    connection.on('JobCompleted', function (data) {
        showToast(
            '✅ Job completed!',
            data.message,
            'success',
            null
        );
        // Update status badge on technician dashboard
        var statusBadge = document.querySelector('.tech-status-badge');
        if (statusBadge) {
            statusBadge.textContent = 'Available';
            statusBadge.className = 'status-badge tech-available';
        }
    });

    // ── Customer handlers ──────────────────────────────────────────

    connection.on('JobAccepted', function (data) {
        showToast(
            '👷 Technician on the way!',
            data.message,
            'success',
            '/Customer/Requests/Index'
        );
        // Update status badge on request card if visible
        updateRequestStatus(data.requestId, 'InProgress');
    });

    connection.on('BillCreated', function (data) {
        showToast(
            '💳 Bill ready — please pay',
            data.message,
            'warning',
            '/Customer/Requests/Index'
        );
        // Update request status + show Pay button
        updateRequestStatus(data.requestId, 'Billed');
        showPayButton(data.requestId);
    });

    // ── Start the connection ───────────────────────────────────────
    connection.start()
        .then(function () {
            console.log('SignalR connected');
            // Join the group matching this user's role
            return connection.invoke('JoinGroup', userRole, userId);
        })
        .then(function () {
            console.log('Joined group: ' + userRole + '-' + userId);
        })
        .catch(function (err) {
            console.error('SignalR connection error:', err);
        });

    // ── Reconnection handling ─────────────────────────────────────
    connection.onreconnected(function () {
        console.log('SignalR reconnected — rejoining group');
        connection.invoke('JoinGroup', userRole, userId);
    });

    // =================================================================
    //  HELPER FUNCTIONS
    // =================================================================

    // Show a toast notification in the top-right corner
    function showToast(title, message, type, linkUrl) {
        // Remove any existing toast
        var existing = document.getElementById('signalr-toast');
        if (existing) existing.remove();

        var colors = {
            success: { bg: '#D1FAE5', border: '#6EE7B7', text: '#065F46' },
            warning: { bg: '#FEF3C7', border: '#FDE68A', text: '#92400E' },
            info: { bg: '#DBEAFE', border: '#93C5FD', text: '#1E40AF' },
            error: { bg: '#FEE2E2', border: '#FCA5A5', text: '#7F1D1D' }
        };
        var c = colors[type] || colors.info;

        var toast = document.createElement('div');
        toast.id = 'signalr-toast';
        toast.style.cssText = [
            'position:fixed',
            'top:20px',
            'right:20px',
            'z-index:9999',
            'min-width:300px',
            'max-width:400px',
            'background:' + c.bg,
            'border:1px solid ' + c.border,
            'border-radius:10px',
            'padding:14px 18px',
            'box-shadow:0 8px 24px rgba(0,0,0,0.12)',
            'animation:slideIn 0.3s ease',
            'cursor:' + (linkUrl ? 'pointer' : 'default')
        ].join(';');

        toast.innerHTML =
            '<div style="display:flex;justify-content:space-between;' +
            'align-items:flex-start;gap:10px">' +
            '<div>' +
            '<p style="font-size:13px;font-weight:600;' +
            'color:' + c.text + ';margin-bottom:3px">' + title + '</p>' +
            '<p style="font-size:12px;color:' + c.text + ';opacity:.85">'
            + message + '</p>' +
            '</div>' +
            '<button onclick="this.parentElement.parentElement.remove()" ' +
            'style="background:none;border:none;font-size:18px;' +
            'cursor:pointer;color:' + c.text + ';padding:0;line-height:1">' +
            '×</button>' +
            '</div>';

        if (linkUrl) {
            toast.addEventListener('click', function (e) {
                if (e.target.tagName !== 'BUTTON')
                    window.location.href = linkUrl;
            });
        }

        document.body.appendChild(toast);

        // Add slide-in animation
        if (!document.getElementById('signalr-style')) {
            var style = document.createElement('style');
            style.id = 'signalr-style';
            style.textContent =
                '@keyframes slideIn{from{transform:translateX(120%);' +
                'opacity:0}to{transform:translateX(0);opacity:1}}';
            document.head.appendChild(style);
        }

        // Auto-dismiss after 6 seconds
        setTimeout(function () {
            if (toast.parentElement) {
                toast.style.opacity = '0';
                toast.style.transition = 'opacity 0.3s';
                setTimeout(function () { toast.remove(); }, 300);
            }
        }, 6000);
    }

    // Update a request status badge on the current page
    function updateRequestStatus(requestId, newStatus) {
        var badges = document.querySelectorAll(
            '[data-request-id="' + requestId + '"] .status-badge');
        badges.forEach(function (badge) {
            badge.textContent = newStatus;
            badge.className = 'status-badge status-' +
                newStatus.toLowerCase();
        });
    }

    // Show Pay Now button on a request card
    function showPayButton(requestId) {
        var card = document.querySelector(
            '[data-request-id="' + requestId + '"]');
        if (!card) return;

        var actions = card.querySelector('.req-actions');
        if (!actions) return;

        // Don't add if already there
        if (card.querySelector('.pay-btn')) return;

        var btn = document.createElement('a');
        btn.className = 'btn-primary btn-sm pay-btn';
        btn.href = '/Customer/Bills/Pay?requestId=' + requestId;
        btn.textContent = 'Pay now';
        actions.appendChild(btn);
    }

    // Increment the pending badge in the admin sidebar
    function updatePendingBadge(delta) {
        var badge = document.getElementById('pending-count-badge');
        if (badge) {
            var current = parseInt(badge.textContent) || 0;
            badge.textContent = current + delta;
        }
    }

    // Refresh admin dashboard stat counts via fetch
    function refreshDashboardCounts() {
        var counters = document.querySelectorAll('[data-stat-count]');
        if (counters.length === 0) return;

        fetch('/Admin/Home/GetCounts', {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                counters.forEach(function (el) {
                    var key = el.dataset.statCount;
                    if (data[key] !== undefined)
                        el.textContent = data[key];
                });
            })
            .catch(function () { });  // fail silently
    }

    // Play a subtle notification sound
    function playNotificationSound() {
        try {
            var ctx = new (window.AudioContext || window.webkitAudioContext)();
            var osc = ctx.createOscillator();
            var gain = ctx.createGain();
            osc.connect(gain);
            gain.connect(ctx.destination);
            osc.frequency.setValueAtTime(880, ctx.currentTime);
            osc.frequency.setValueAtTime(660, ctx.currentTime + 0.1);
            gain.gain.setValueAtTime(0.1, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(
                0.001, ctx.currentTime + 0.4);
            osc.start(ctx.currentTime);
            osc.stop(ctx.currentTime + 0.4);
        } catch (e) { }  // fail silently if audio not available
    }

})();