/*
  ثيم متجر رمز — JS (النسخة المقروءة)
  للصق بالمتجر استخدم rmz-theme.min.js — خانة الـ JS بالمتجر تقطع الكود الطويل
  (تقريباً بعد 4 آلاف حرف)، والنسخة المضغوطة أصغر بكثير من الحد.

  - خطوط الخلفية صارت بالـ CSS (ما تحتاج JS).
  - المتجر مبني بـ React: ما ننقل ولا نحذف أي عنصر من عناصره، بس نضيف.
*/
(function () {
  var reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;

  /* 1) شاشة الترحيب — مرة وحدة كل جلسة */
  function splash() {
    try {
      if (sessionStorage.getItem('st_splash')) return;
      sessionStorage.setItem('st_splash', '1');
    } catch (e) { return; }
    var el = document.createElement('div');
    el.id = 'rmz-splash';
    var text = document.createElement('span');
    text.className = 'rmz-splash-text';
    text.textContent = document.title;
    el.appendChild(text);
    document.body.appendChild(el);
    setTimeout(function () {
      el.classList.add('rmz-hide');
      setTimeout(function () { el.remove(); }, 650);
    }, reduced ? 200 : 1600);
  }

  /* 2) شريط الأقسام تحت الهيدر (عنصر جديد، ما نلمس الهيدر) */
  function catBar() {
    var header = document.querySelector('header');
    var bar = document.querySelector('.rmz-cat-bar');
    if (!bar && header) {
      var seen = {};
      var links = [['/products', 'جميع المنتجات']];
      document.querySelectorAll('a[href*="/category/"]').forEach(function (a) {
        var label = a.textContent.trim();
        if (label && !seen[a.pathname]) {
          seen[a.pathname] = 1;
          links.push([a.pathname, label]);
        }
      });
      if (links.length < 2) return;
      bar = document.createElement('nav');
      bar.className = 'rmz-cat-bar';
      links.forEach(function (l) {
        var a = document.createElement('a');
        a.className = 'rmz-cat-link';
        a.href = l[0];
        a.textContent = l[1];
        bar.appendChild(a);
      });
      header.after(bar);
    }
    if (bar) bar.querySelectorAll('a').forEach(function (a) {
      a.classList.toggle('rmz-active', a.pathname === location.pathname);
    });
  }

  /* 3) عدّ تصاعدي للأرقام مثل "1,250+" — نعدّل نفس النص مكانه */
  function counters() {
    if (reduced) return;
    var walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    var node;
    while ((node = walker.nextNode())) {
      if (!/^\s*[\d,]+\+\s*$/.test(node.nodeValue) || node.st) continue;
      node.st = 1;
      (function (node) {
        var original = node.nodeValue;
        var target = parseInt(original.replace(/\D/g, ''), 10);
        var obs = new IntersectionObserver(function (entries) {
          if (!entries[0].isIntersecting) return;
          obs.disconnect();
          var start;
          requestAnimationFrame(function step(ts) {
            start = start || ts;
            var p = Math.min((ts - start) / 1200, 1);
            node.nodeValue = p < 1 ? Math.floor(target * p).toLocaleString('en-US') + '+' : original;
            if (p < 1) requestAnimationFrame(step);
          });
        }, { threshold: 0.4 });
        obs.observe(node.parentElement);
      })(node);
    }
  }

  /* 4) أي طبقة بيضاء كبيرة تغطي الخلفية نخليها شفافة (نغيّر اللون بس) */
  function clearWhite() {
    document.querySelectorAll('body div, body section, body main').forEach(function (el) {
      if (el.st || el.closest('header, footer, .navbar, [role="dialog"]')) return;
      var r = el.getBoundingClientRect();
      if (r.width < innerWidth * 0.6 || r.height < 300) return;
      var cs = getComputedStyle(el);
      var m = cs.backgroundColor.match(/[\d.]+/g);
      if (cs.position !== 'fixed' && m && m[0] > 235 && m[1] > 235 && m[2] > 235 && (m[3] === undefined || m[3] > 0.5)) {
        el.st = 1;
        el.style.setProperty('background-color', 'transparent', 'important');
      }
    });
  }

  function run(fn) {
    try { fn(); } catch (e) { console.warn('[theme]', e); }
  }

  function start() {
    run(splash);
    run(catBar);
    run(counters);
    run(clearWhite);
    // المتجر يتنقل بين الصفحات بدون إعادة تحميل، فنعيد التجهيز لما يتغير المحتوى
    var timer = 0;
    new MutationObserver(function () {
      if (timer) return;
      timer = setTimeout(function () {
        timer = 0;
        run(catBar);
        run(counters);
        run(clearWhite);
      }, 400);
    }).observe(document.body, { childList: true, subtree: true });
  }

  if (document.readyState === 'complete') start();
  else addEventListener('load', start);
})();
