/*
  ثيم المتجر — JS (النسخة المقروءة)
  للصق بالمتجر استخدم store.min.js (خانة الـ JS بالمتجر تقطع الكود الطويل).
  قاعدة: ما ننقل ولا نحذف أي عنصر من عناصر المتجر، بس نضيف عناصرنا.
*/
(function () {
  /* ================= الإعدادات ================= */
  var IMG = 'https://cdn.jsdelivr.net/gh/saadharazi/saad@9a00e02b83988e5ce26fe22e889cc05b84458063/theme/img/';
  var RAW = 'https://raw.githubusercontent.com/saadharazi/saad/9a00e02b83988e5ce26fe22e889cc05b84458063/theme/img/';
  var TOP_IMG = 'top-banner.webp'; // البنر العلوي (صورة أو GIF)
  var PAY_IMG = 'payments.webp';   // بنر طرق الدفع
  var CART = /أضف للسلة|اضف للسلة|add to cart/i;
  var REVIEWS = /تقييم|آراء|اراء|قالوا|عملاؤنا|عملائنا|review|testimonial/i;

  // الصفحة الرئيسية بس: "/" أو "/ar" أو "/en"
  function isHome() {
    return /^\/(ar|en)?\/?$/.test(location.pathname);
  }

  /* ============ 1) شريط الأقسام (للجوال) ============ */
  function catBar() {
    var header = document.querySelector('header');
    var bar = document.querySelector('.st-cats');
    if (!bar && header) {
      var links = document.querySelectorAll('header a[href*="/category/"]');
      if (!links.length) return;
      bar = document.createElement('nav');
      bar.className = 'st-cats';
      addLink(bar, '/products', 'جميع المنتجات');
      links.forEach(function (a) {
        var text = a.textContent.trim();
        if (text && !bar.querySelector('[href="' + a.pathname + '"]')) addLink(bar, a.pathname, text);
      });
      header.after(bar);
    }
    if (bar) bar.querySelectorAll('a').forEach(function (a) {
      a.classList.toggle('st-on', a.pathname == location.pathname);
    });
  }

  function addLink(bar, href, text) {
    var a = document.createElement('a');
    a.href = href;
    a.textContent = text;
    bar.appendChild(a);
  }

  /* ============ 2) البنرات (بالرئيسية بس) ============ */
  // العلوي تحت الهيدر، وطرق الدفع فوق قسم التقييمات
  function banners() {
    var home = isHome();
    var header = document.querySelector('header');
    var top = document.getElementById('st-top');
    var pay = document.getElementById('st-pay');

    if (home && !top && header) {
      top = makeBanner('st-top', TOP_IMG, 'eager');
      (document.querySelector('.st-cats') || header).after(top);
    }
    if (home && !pay) {
      var title = reviewsTitle();
      if (title) rowOf(title).before(pay = makeBanner('st-pay', PAY_IMG, 'lazy'));
    }
    if (top) top.hidden = !home;
    if (pay) pay.hidden = !home;
  }

  // عنوان قسم التقييمات: أكبر نص قصير ظاهر فيه كلمة تقييم/آراء/...
  function reviewsTitle() {
    var best, size = 17; // حجم عنوان (18px وأكثر)، عشان نص التقييم الصغير بالكروت ما ينحسب
    document.querySelectorAll('body *').forEach(function (e) {
      var text = e.textContent.trim();
      if (text.length > 40 || !e.offsetWidth || !REVIEWS.test(text) ||
          e.closest('header, footer, a, button, .st-banner')) return;
      var fs = parseFloat(getComputedStyle(e).fontSize);
      if (fs > size) { size = fs; best = e; }
    });
    return best;
  }

  // أول صف عريض يحتوي العنوان (نحط البنر قبله)
  function rowOf(e) {
    while (e.parentElement != document.body && e.offsetWidth < innerWidth * 0.5) e = e.parentElement;
    return e;
  }

  function makeBanner(id, file, loading) {
    var box = document.createElement('div');
    var img = document.createElement('img');
    box.id = id;
    box.className = 'st-banner';
    img.alt = '';
    img.loading = loading;
    img.decoding = 'async';
    img.onerror = function () { img.onerror = null; img.src = RAW + file; };
    img.src = IMG + file;
    box.appendChild(img);
    return box;
  }

  /* ====== 3) المنتج اللي لحاله بقسمه يجي بالنص ====== */
  function centerLone() {
    document.querySelectorAll('button, a').forEach(function (btn) {
      if (btn.st || !btn.offsetWidth || !CART.test(btn.textContent)) return;
      btn.st = 1;
      // الكرت = أصغر عنصر فيه صورة المنتج والزر، ثم أغلفته اللي بنفس عرضه تقريباً
      var card = btn, list;
      while (card.parentElement != document.body && !card.querySelector('img')) card = card.parentElement;
      while ((list = card.parentElement) != document.body && list.offsetWidth < card.offsetWidth * 1.2) card = list;
      var carts = [].filter.call(list.querySelectorAll('button, a'), function (b) { return CART.test(b.textContent); });
      if (list == document.body || carts.length != 1) return;
      // نوسّط الكرت نفسه بس، ترتيب القسم ما يتغير
      card.style.cssText += ';width:' + card.offsetWidth + 'px;max-width:100%;flex:none;grid-column:1/-1;' +
        'margin-left:auto!important;margin-right:auto!important';
    });
  }

  /* ====== 4) الطبقات البيضاء الكبيرة اللي تغطي الخلفية ====== */
  function clearWhite() {
    document.querySelectorAll('body div, body section').forEach(function (e) {
      if (e.st || e.closest('header, footer, [role=dialog]')) return;
      var r = e.getBoundingClientRect();
      if (r.width < innerWidth * 0.6 || r.height < 150) return;
      var cs = getComputedStyle(e), c = cs.backgroundColor.match(/[\d.]+/g);
      if (cs.position == 'fixed' || !c || c[0] < 235 || c[1] < 235 || c[2] < 235 || c[3] < 0.5) return;
      e.st = 1;
      e.style.setProperty('background-color', 'transparent', 'important');
    });
  }

  /* ================= التشغيل ================= */
  var tasks = [catBar, banners, centerLone, clearWhite];

  function runAll() {
    tasks.forEach(function (f) {
      try { f(); } catch (e) {}
    });
  }

  function start() {
    runAll();
    // أقسام رمز تحمّل متأخر: نعيد كل ثانيتين لمدة 20 ثانية
    var n = 0, timer = setInterval(function () {
      runAll();
      if (++n > 10) clearInterval(timer);
    }, 2000);
    // تنقّل المتجر بدون إعادة تحميل: نعيد عند تغيّر الصفحة (مرة كل نص ثانية بالكثير)
    var busy = 0;
    new MutationObserver(function () {
      if (!busy) busy = setTimeout(function () { busy = 0; runAll(); }, 500);
    }).observe(document.body, { childList: true, subtree: true });
  }

  if (document.readyState == 'complete') start();
  else addEventListener('load', start);
})();
