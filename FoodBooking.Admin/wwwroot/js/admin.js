// Admin helpers: confirm dialogs, new-order polling with sound alert (kitchen alert), toasts
(function ($) {
    const token = () => $('input[name="__RequestVerificationToken"]').first().val();
    window.adminPost = (url, data) => $.ajax({ url, method: 'POST', data, headers: { 'RequestVerificationToken': token(), 'X-Requested-With': 'XMLHttpRequest' } });

    window.adminToast = function (msg, type) {
        const $t = $(`<div class="toast text-bg-${type || 'dark'} border-0"><div class="d-flex"><div class="toast-body"></div><button class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button></div></div>`);
        $t.find('.toast-body').text(msg);
        $('#toasts').append($t);
        new bootstrap.Toast($t[0], { delay: 8000 }).show();
    };

    // Confirm before destructive actions: <form data-confirm="Are you sure?">
    $(document).on('submit', 'form[data-confirm]', function (e) { if (!confirm($(this).data('confirm'))) e.preventDefault(); });

    // ---- New order alert ----
    let soundOn = localStorage.getItem('fb-sound') !== 'off';
    const $sound = $('#soundToggle');
    const renderSound = () => $sound.find('i').attr('class', soundOn ? 'bi bi-volume-up' : 'bi bi-volume-mute');
    renderSound();
    $sound.on('click', () => { soundOn = !soundOn; localStorage.setItem('fb-sound', soundOn ? 'on' : 'off'); renderSound(); if (soundOn) beep(); });

    let ctx;
    function beep() {
        try {
            ctx = ctx || new (window.AudioContext || window.webkitAudioContext)();
            [0, 0.25, 0.5].forEach((t, i) => {
                const o = ctx.createOscillator(), g = ctx.createGain();
                o.type = 'sine'; o.frequency.value = i === 2 ? 1320 : 880;
                g.gain.setValueAtTime(0.0001, ctx.currentTime + t);
                g.gain.exponentialRampToValueAtTime(0.4, ctx.currentTime + t + 0.02);
                g.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + t + 0.22);
                o.connect(g); g.connect(ctx.destination);
                o.start(ctx.currentTime + t); o.stop(ctx.currentTime + t + 0.25);
            });
        } catch (e) { }
    }
    window.adminBeep = beep;

    let lastId = parseInt(sessionStorage.getItem('fb-last-order') || '0');
    function poll() {
        $.getJSON('/dashboard/poll').done(r => {
            const $b = $('[data-new-orders]');
            $b.text(r.count).toggleClass('d-none', r.count === 0);
            document.title = (r.count ? `(${r.count}) ` : '') + document.title.replace(/^\(\d+\) /, '');
            if (r.latestId > lastId) {
                if (lastId > 0 || sessionStorage.getItem('fb-last-order') === null) {
                    if (soundOn) beep();
                    const o = r.orders[0];
                    if (o) adminToast(`🔔 New order #${o.orderNumber} from ${o.customerName} — ${ADMIN.currency}${o.total.toFixed(2)}`, 'danger');
                    $(document).trigger('admin:new-order', r);
                }
                lastId = r.latestId;
                sessionStorage.setItem('fb-last-order', lastId);
            }
        });
    }
    poll();
    setInterval(poll, 15000);

    // Image preview for file inputs: <input type=file data-preview="#img">
    $(document).on('change', 'input[type=file][data-preview]', function () {
        const f = this.files && this.files[0];
        if (f) $($(this).data('preview')).attr('src', URL.createObjectURL(f)).removeClass('d-none');
    });
})(jQuery);
