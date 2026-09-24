(function () {
  // روابط البنرات: تقدر تغيّرها لرابط صورة أو GIF من متجرك
  var IMG = 'https://cdn.jsdelivr.net/gh/saadharazi/saad@9a00e02b83988e5ce26fe22e889cc05b84458063/theme/img/';
  // مصدر احتياطي لو jsDelivr ما حمّل
  var RAW = 'https://raw.githubusercontent.com/saadharazi/saad/9a00e02b83988e5ce26fe22e889cc05b84458063/theme/img/';
  var TOP = IMG + 'top-banner.webp';
  var PAY = IMG + 'payments.webp';

  // شريط الأقسام تحت الهيدر، من روابط الأقسام الموجودة بالمتجر
  function cats() {
    var h = document.querySelector('header');
    var bar = document.querySelector('.st-cats');
    if (!bar && h) {
      var links = document.querySelectorAll('header a[href*="/category/"]');
      if (!links.length) return;
      bar = document.createElement('nav');
      bar.className = 'st-cats';
      add(bar, '/products', 'جميع المنتجات');
      links.forEach(function (a) {
        var t = a.textContent.trim();
        if (t && !bar.querySelector('[href="' + a.pathname + '"]')) add(bar, a.pathname, t);
      });
      h.after(bar);
    }
    if (bar) bar.querySelectorAll('a').forEach(function (a) {
      a.classList.toggle('st-on', a.pathname === location.pathname);
    });
  }

  function add(bar, href, text) {
    var a = document.createElement('a');
    a.href = href;
    a.textContent = text;
    bar.appendChild(a);
  }

  // البنرات (بالصفحة الرئيسية بس): المتحرك تحت الهيدر، وطرق الدفع فوق التقييمات
  // عنوان قسم التقييمات: أكبر نص قصير فيه "تقييم/آراء/reviews" وحجمه حجم عنوان
  // (18px أو أكثر)، عشان نص التقييم الصغير بكروت المنتجات ما ينحسب
  function reviewsTitle() {
    var best, size = 17;
    document.querySelectorAll('body *').forEach(function (e) {
      var t = e.children.length ? '' : e.textContent.trim();
      if (!t || t.length > 40 || !e.offsetWidth || !/تقييم|آراء|اراء|review/i.test(t) || e.closest('header, footer, .st-banner')) return;
      var f = parseFloat(getComputedStyle(e).fontSize);
      if (f > size) { size = f; best = e; }
    });
    return best;
  }

  function banners() {
    var h = document.querySelector('header');
    var top = document.getElementById('st-top'), pay = document.getElementById('st-pay');
    var title = !pay && reviewsTitle();
    // الرئيسية: "/" أو "/ar" أو "/en"، أو أي صفحة فيها قسم التقييمات
    var home = /^\/(ar|en)?\/?$/.test(location.pathname) || !!title || !!pay;
    if (home && !top && h) {
      top = banner('st-top', TOP, 'eager');
      (document.querySelector('.st-cats') || h).after(top);
    }
    if (title) {
      // نطلع من العنوان لأول صف عريض، ونحط البنر قبله (فوق التقييمات)
      var spot = title;
      while (spot.parentElement != document.body && spot.offsetWidth < innerWidth * 0.5) spot = spot.parentElement;
      spot.before(banner('st-pay', PAY, 'lazy'));
    }
    if (top) top.hidden = !home;
  }

  // المنتج اللي لحاله بقسمه يجي بالنص (نعرف الكرت من زر "أضف للسلة")
  function isCart(b) {
    return /أضف للسلة|اضف للسلة|add to cart/i.test(b.textContent);
  }
  function lone() {
    document.querySelectorAll('button, a').forEach(function (b) {
      if (b.st || !b.offsetWidth || !isCart(b)) return;
      b.st = 1;
      // الكرت = أصغر عنصر فيه صورة المنتج والزر، وبعدها أغلفته اللي بنفس عرضه تقريباً
      var card = b, list;
      while (card.parentElement != document.body && !card.querySelector('img')) card = card.parentElement;
      while ((list = card.parentElement) != document.body && list.offsetWidth < card.offsetWidth * 1.2) card = list;
      if (list == document.body || [].filter.call(list.querySelectorAll('button, a'), isCart).length != 1) return;
      // نوسّط الكرت نفسه بس، بدون ما نغيّر ترتيب القسم
      card.style.cssText += ';width:' + card.offsetWidth + 'px;max-width:100%;flex:none;grid-column:1/-1;' +
        'margin-left:auto!important;margin-right:auto!important';
    });
  }

  function banner(id, src, load) {
    var d = document.createElement('div'), m;
    d.id = id;
    d.className = 'st-banner';
    m = document.createElement('img');
    m.alt = '';
    m.loading = load;
    m.decoding = 'async';
    m.onerror = function () {
      m.onerror = null;
      m.src = src.replace(IMG, RAW);
    };
    m.src = src;
    d.appendChild(m);
    return d;
  }

  // أي طبقة بيضاء كبيرة تغطي الخلفية تصير شفافة (الكروت أصغر فما تتأثر)
  function unwhite() {
    document.querySelectorAll('body div, body section').forEach(function (el) {
      if (el.st || el.closest('header, footer, [role=dialog]')) return;
      var r = el.getBoundingClientRect();
      if (r.width < innerWidth * 0.6 || r.height < 150) return;
      var c = getComputedStyle(el), v = c.backgroundColor.match(/[\d.]+/g);
      if (c.position == 'fixed' || !v || v[0] < 235 || v[1] < 235 || v[2] < 235 || v[3] < 0.5) return;
      el.st = 1;
      el.style.setProperty('background-color', 'transparent', 'important');
    });
  }

  function safe(f) {
    try { f(); } catch (e) {}
  }

  function start() {
    [cats, banners, lone, unwhite].forEach(safe);
    // أقسام رمز (مثل التقييمات) تحمّل متأخر: نعيد الفحص كل ثانيتين لمدة 20 ثانية
    var n = 0, iv = setInterval(function () {
      [banners, lone, unwhite].forEach(safe);
      if (++n > 10) clearInterval(iv);
    }, 2000);
    // المتجر يغيّر الصفحة بدون إعادة تحميل: نعيد الفحص (مرة كل نص ثانية بالكثير)
    var busy = 0;
    new MutationObserver(function () {
      if (busy) return;
      busy = setTimeout(function () {
        busy = 0;
        [cats, banners, lone, unwhite].forEach(safe);
      }, 500);
    }).observe(document.body, { childList: true, subtree: true });
  }

  if (document.readyState == 'complete') start();
  else addEventListener('load', start);
})();
