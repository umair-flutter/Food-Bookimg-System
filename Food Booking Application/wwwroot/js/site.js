// Foodie Hub front-end helpers
window.FB = (function ($) {
    const token = () => $('input[name="__RequestVerificationToken"]').first().val();

    function post(url, data) {
        return $.ajax({
            url, method: 'POST', data,
            headers: { 'RequestVerificationToken': token(), 'X-Requested-With': 'XMLHttpRequest' }
        });
    }

    function toast(message, type) {
        const icon = type === 'success' ? 'bi-check-circle-fill' : type === 'danger' ? 'bi-exclamation-circle-fill' : 'bi-info-circle-fill';
        const $t = $(`<div class="toast align-items-center text-bg-${type || 'dark'} border-0" role="alert">
            <div class="d-flex"><div class="toast-body"><i class="bi ${icon} me-2"></i></div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button></div></div>`);
        $t.find('.toast-body').append(document.createTextNode(message));
        $('#toasts').append($t);
        const t = new bootstrap.Toast($t[0], { delay: 3500 });
        t.show();
        $t.on('hidden.bs.toast', () => $t.remove());
    }

    function setCount(n) {
        $('[data-cart-count]').text(n).addClass('bump');
        setTimeout(() => $('[data-cart-count]').removeClass('bump'), 400);
    }

    function loadMiniCart() {
        $('[data-mini-cart]').load('/cart/mini');
    }

    function addToCart(data, $btn) {
        $btn && $btn.addClass('loading');
        return post('/cart/add', data).done(r => {
            if (r.ok) {
                setCount(r.count);
                toast(r.message, 'success');
                loadMiniCart();
                bootstrap.Offcanvas.getOrCreateInstance('#miniCart').show();
            } else if (r.redirect) {
                window.location = r.redirect;
            } else toast(r.message, 'danger');
        }).fail(() => toast('Something went wrong. Please try again.', 'danger'))
          .always(() => $btn && $btn.removeClass('loading'));
    }

    function money(n) { return (window.FB_CURRENCY || '$') + n.toFixed(2); }

    // ---- bindings ----
    $(document).on('click', '[data-add-to-cart]', function (e) {
        e.preventDefault();
        addToCart({ productId: $(this).data('add-to-cart'), quantity: 1 }, $(this));
    });

    $(document).on('submit', '#productForm', function (e) {
        e.preventDefault();
        const $f = $(this);
        if (!this.checkValidity()) { this.reportValidity(); return; }
        addToCart($f.serialize(), $f.find('[type=submit]'));
    });

    $('#miniCart').on('show.bs.offcanvas', loadMiniCart);

    // quantity steppers
    $(document).on('click', '.qty [data-step]', function () {
        const $i = $(this).closest('.qty').find('input');
        const v = Math.max(parseInt($i.attr('min') || '0'), Math.min(50, (parseInt($i.val()) || 1) + parseInt($(this).data('step'))));
        $i.val(v).trigger('change');
    });

    // cart page / mini cart updates
    function applyTotals(r) {
        if (!r.ok) { toast(r.message || 'Error', 'danger'); return; }
        if (r.empty) { window.location.reload(); return; }
        setCount(r.count);
        $('[data-sum=subtotal]').text(r.subTotal);
        $('[data-sum=discount]').text('-' + r.discount);
        $('[data-sum=tax]').text(r.tax);
        $('[data-sum=delivery]').text(r.delivery);
        $('[data-sum=tip]').text(r.tip);
        $('[data-sum=total]').text(r.total);
        $('[data-row=discount]').toggleClass('d-none', !r.hasDiscount);
        $('[data-min-warning]').toggleClass('d-none', !r.belowMinimum);
        $('[data-checkout-btn]').toggleClass('disabled', r.belowMinimum);
        for (const k in r.lines) $(`[data-line-total="${k}"]`).text(r.lines[k]);
        if (r.message) toast(r.message, 'success');
        if (r.couponMessage !== undefined) $('[data-coupon-msg]').text(r.couponMessage || '');
    }

    $(document).on('change', '[data-cart-qty]', function () {
        post('/cart/update', { key: $(this).data('cart-qty'), quantity: $(this).val() }).done(r => { applyTotals(r); loadMiniCartIfOpen(); });
    });
    $(document).on('click', '[data-cart-remove]', function () {
        const key = $(this).data('cart-remove');
        post('/cart/remove', { key }).done(r => {
            $(`[data-line="${key}"]`).slideUp(200, function () { $(this).remove(); });
            applyTotals(r); loadMiniCartIfOpen();
        });
    });
    function loadMiniCartIfOpen() { if ($('#miniCart').hasClass('show')) loadMiniCart(); }

    $(document).on('submit', '#couponForm', function (e) {
        e.preventDefault();
        post('/cart/coupon', $(this).serialize()).done(r => {
            if (!r.ok) { $('[data-coupon-msg]').text(r.message).addClass('text-danger'); return; }
            $('[data-coupon-msg]').removeClass('text-danger');
            window.location.reload();
        });
    });
    $(document).on('click', '[data-remove-coupon]', function () {
        post('/cart/coupon', { code: '' }).done(() => window.location.reload());
    });
    $(document).on('click', '[data-tip]', function () {
        $('[data-tip]').removeClass('active btn-dark').addClass('btn-outline-dark');
        $(this).addClass('active btn-dark').removeClass('btn-outline-dark');
        post('/cart/tip', { tip: $(this).data('tip') }).done(r => { applyTotals(r); $(document).trigger('fb:totals', r); });
    });

    // newsletter
    $(document).on('submit', '[data-newsletter]', function (e) {
        e.preventDefault();
        const $f = $(this);
        post('/newsletter', $f.serialize()).done(r => { toast(r.message, r.ok ? 'success' : 'danger'); if (r.ok) $f[0].reset(); });
    });

    // search suggestions
    let timer;
    $(document).on('input', '[data-suggest]', function () {
        const $i = $(this), $box = $i.siblings('.suggest-box'), q = $i.val().trim();
        clearTimeout(timer);
        if (q.length < 2) { $box.hide(); return; }
        timer = setTimeout(() => {
            $.getJSON('/search/suggest', { q }).done(items => {
                $box.empty();
                if (!items.length) { $box.hide(); return; }
                items.forEach(it => {
                    const $a = $('<a>').attr('href', it.url);
                    $a.append($('<img>').attr('src', it.image).attr('alt', ''));
                    $a.append($('<div class="flex-grow-1">').append($('<div class="fw-semibold">').text(it.name)).append($('<small class="text-primary">').text(it.price)));
                    $box.append($a);
                });
                $box.append($('<a class="justify-content-center small text-muted">').attr('href', '/search?q=' + encodeURIComponent(q)).text('See all results'));
                $box.show();
            });
        }, 220);
    });
    $(document).on('click', e => { if (!$(e.target).closest('.search-form').length) $('.suggest-box').hide(); });

    // gallery + zoom
    $(document).on('click', '.gallery-thumbs img', function () {
        const src = $(this).data('full');
        $('.gallery-thumbs img').removeClass('active');
        $(this).addClass('active');
        $('#mainImage').attr('src', src);
        $('.zoom-lens').css('background-image', `url('${src}')`);
    });
    $(document).on('mousemove', '.gallery-main', function (e) {
        const r = this.getBoundingClientRect();
        const x = ((e.clientX - r.left) / r.width) * 100, y = ((e.clientY - r.top) / r.height) * 100;
        $(this).addClass('zooming').find('.zoom-lens').css('background-position', `${x}% ${y}%`);
    }).on('mouseleave', '.gallery-main', function () { $(this).removeClass('zooming'); });

    // product option price live update
    function updateDetailPrice() {
        const $f = $('#productForm');
        if (!$f.length) return;
        let unit = parseFloat($f.data('base'));
        $f.find('input[data-adj]:checked').each(function () { unit += parseFloat($(this).data('adj')); });
        const qty = parseInt($f.find('[name=quantity]').val()) || 1;
        $('[data-detail-price]').text(money(unit));
        $('[data-detail-total]').text(money(unit * qty));
    }
    $(document).on('change', '#productForm input', updateDetailPrice);
    $(updateDetailPrice);

    // back to top
    $(window).on('scroll', () => $('.back-to-top').toggleClass('show', window.scrollY > 500));
    $(document).on('click', '.back-to-top', () => window.scrollTo({ top: 0, behavior: 'smooth' }));

    return { toast, post, setCount, addToCart, money };
})(jQuery);
