(function () {
  // روابط البنرات: تقدر تغيّرها لرابط صورة/GIF/فيديو (mp4 أو webm) من متجرك
  var IMG = 'https://cdn.jsdelivr.net/gh/saadharazi/saad@9a00e02b83988e5ce26fe22e889cc05b84458063/theme/img/';
  var TOP = IMG + 'top-banner.webp';
  var PAY = IMG + 'payments.webp';

  var calm = matchMedia('(prefers-reduced-motion: reduce)').matches;

  // شاشة الترحيب: مرة وحدة كل جلسة
  function splash() {
    try {
      if (sessionStorage.st) return;
      sessionStorage.st = 1;
    } catch (e) { return; }
    var s = document.createElement('div');
    s.id = 'st-splash';
    s.innerHTML = '<span></span>';
    s.firstChild.textContent = document.title;
    document.body.appendChild(s);
    setTimeout(function () {
      s.className = 'st-out';
      setTimeout(function () { s.remove(); }, 700);
    }, calm ? 200 : 1600);
  }

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
  function banners() {
    var home = location.pathname == '/', h = document.querySelector('header');
    var top = document.getElementById('st-top'), pay = document.getElementById('st-pay');
    if (home && !top && h) {
      top = banner('st-top', TOP, 'eager');
      (document.querySelector('.st-cats') || h).after(top);
    }
    if (home && !pay) {
      var head = [].find.call(document.querySelectorAll('h1, h2, h3, h4'), function (e) {
        return /تقييم|آراء|اراء|review/i.test(e.textContent) && !e.closest('header, footer');
      });
      var spot = head ? head.closest('section') || head.parentElement : document.querySelector('footer');
      if (spot) spot.before(pay = banner('st-pay', PAY, 'lazy'));
    }
    if (top) top.hidden = !home;
    if (pay) pay.hidden = !home;
  }
  function banner(id, src, load) {
    var d = document.createElement('div'), m;
    d.id = id;
    d.className = 'st-banner';
    if (/\.(mp4|webm)$/i.test(src)) {
      m = document.createElement('video');
      m.autoplay = m.loop = m.muted = m.playsInline = true;
    } else {
      m = document.createElement('img');
      m.alt = '';
      m.loading = load;
      m.decoding = 'async';
    }
    m.src = src;
    d.appendChild(m);
    return d;
  }

  // الأرقام مثل "1,250+" تعدّ تصاعدياً لما تظهر (نفس النص، مكانه)
  function count() {
    if (calm) return;
    var w = document.createTreeWalker(document.body, 4), t;
    while ((t = w.nextNode())) {
      if (t.st || !/^\s*[\d,]+\+\s*$/.test(t.data)) continue;
      t.st = 1;
      watch(t);
    }
  }
  function watch(t) {
    var end = t.data, max = +end.replace(/\D/g, '');
    var io = new IntersectionObserver(function (e) {
      if (!e[0].isIntersecting) return;
      io.disconnect();
      var t0;
      requestAnimationFrame(function step(now) {
        t0 = t0 || now;
        var p = Math.min((now - t0) / 1200, 1);
        t.data = p < 1 ? Math.floor(max * p).toLocaleString('en-US') + '+' : end;
        if (p < 1) requestAnimationFrame(step);
      });
    }, { threshold: 0.4 });
    io.observe(t.parentElement);
  }

  // أي طبقة بيضاء كبيرة تغطي الخلفية تصير شفافة (الكروت أصغر فما تتأثر)
  function unwhite() {
    document.querySelectorAll('body div, body section').forEach(function (el) {
      if (el.st || el.closest('header, footer, [role=dialog]')) return;
      var r = el.getBoundingClientRect();
      if (r.width < innerWidth * 0.6 || r.height < 300) return;
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
    safe(splash);
    [cats, banners, count, unwhite].forEach(safe);
    // المتجر يغيّر الصفحة بدون إعادة تحميل: نعيد الفحص (مرة كل نص ثانية بالكثير)
    var busy = 0;
    new MutationObserver(function () {
      if (busy) return;
      busy = setTimeout(function () {
        busy = 0;
        [cats, banners, count, unwhite].forEach(safe);
      }, 500);
    }).observe(document.body, { childList: true, subtree: true });
  }

  if (document.readyState == 'complete') start();
  else addEventListener('load', start);
})();
