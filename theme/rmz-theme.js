/*
  ثيم متجر رمز — JS
  قاعدة مهمة: متجر رمز مبني بـ React، فالكود هذا ما ينقل ولا يحذف أي عنصر
  من عناصر المتجر (نقلها يكسر الصفحة). بس يضيف عناصر جديدة خاصة فيه.
*/
(function () {
  var prefersReduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  var CONFIG = {
    logoUrl: '', // حط رابط شعارك هنا لو تبيه بشاشة الترحيب، وإلا بيستخدم اسم المتجر
    splashDuration: 1600 // مدة ظهور شاشة الترحيب بالمللي ثانية
  };

  function sessionGet(key) {
    try { return sessionStorage.getItem(key); } catch (e) { return null; }
  }
  function sessionSet(key, value) {
    try { sessionStorage.setItem(key, value); } catch (e) {}
  }

  /* ====== 1) شاشة الترحيب — مرة وحدة كل جلسة ====== */
  function initSplash() {
    if (sessionGet('rmz_splash_shown')) return;
    sessionSet('rmz_splash_shown', '1');

    var splash = document.createElement('div');
    splash.id = 'rmz-splash';
    if (CONFIG.logoUrl) {
      var img = document.createElement('img');
      img.src = CONFIG.logoUrl;
      img.alt = document.title;
      splash.appendChild(img);
    } else {
      var text = document.createElement('span');
      text.className = 'rmz-splash-text';
      text.textContent = document.title || 'المتجر';
      splash.appendChild(text);
    }
    document.body.appendChild(splash);

    setTimeout(function () {
      splash.classList.add('rmz-hide');
      setTimeout(function () { splash.remove(); }, 650);
    }, prefersReduced ? 200 : CONFIG.splashDuration);
  }

  /* ====== 2) عمق خفيف للشعار يتبع الماوس ====== */
  function initParallax() {
    if (prefersReduced) return;
    var raf = null;
    document.addEventListener('mousemove', function (e) {
      if (raf) return;
      raf = requestAnimationFrame(function () {
        var root = document.documentElement.style;
        root.setProperty('--st-px', ((e.clientX / window.innerWidth - 0.5) * 10).toFixed(2));
        root.setProperty('--st-py', ((e.clientY / window.innerHeight - 0.5) * 10).toFixed(2));
        raf = null;
      });
    });
  }

  /* ====== 3) شريط الأقسام تحت الهيدر (عنصر جديد، ما نلمس الهيدر) ====== */
  function buildCatBar() {
    if (document.querySelector('.rmz-cat-bar')) return;
    var header = document.querySelector('header');
    if (!header || !header.parentNode) return;

    var seen = {};
    var cats = Array.from(document.querySelectorAll('a[href*="/category/"]')).filter(function (a) {
      if (a.closest('.rmz-cat-bar')) return false;
      var path = a.pathname;
      if (seen[path] || !a.textContent.trim()) return false;
      seen[path] = true;
      return true;
    });
    if (!cats.length) return;

    var bar = document.createElement('nav');
    bar.className = 'rmz-cat-bar';
    bar.setAttribute('aria-label', 'الأقسام');

    function addLink(href, label) {
      var link = document.createElement('a');
      link.href = href;
      link.className = 'rmz-cat-link';
      link.textContent = label;
      bar.appendChild(link);
    }
    addLink('/products', 'جميع المنتجات');
    cats.forEach(function (a) { addLink(a.pathname, a.textContent.trim()); });

    header.parentNode.insertBefore(bar, header.nextSibling);
    markActiveCat();
  }

  function markActiveCat() {
    document.querySelectorAll('.rmz-cat-link').forEach(function (link) {
      link.classList.toggle('rmz-active', link.pathname === location.pathname);
    });
  }

  /* ====== 4) عدّ تصاعدي للأرقام الحقيقية (مكانها، بدون نقلها) ====== */
  function animateCounters() {
    if (prefersReduced || !('IntersectionObserver' in window)) return;

    var walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    var nodes = [];
    while (walker.nextNode()) {
      var node = walker.currentNode;
      if (/^\s*[\d,]+\+\s*$/.test(node.nodeValue) && node.parentElement &&
          !node.parentElement.hasAttribute('data-st-counted')) {
        nodes.push(node);
      }
    }

    nodes.forEach(function (node) {
      var el = node.parentElement;
      el.setAttribute('data-st-counted', '1');
      var original = node.nodeValue;
      var target = parseInt(original.replace(/[^\d]/g, ''), 10);
      if (!target) return;

      var obs = new IntersectionObserver(function (entries) {
        if (!entries[0].isIntersecting) return;
        obs.disconnect();
        var start = null;
        function step(ts) {
          if (!start) start = ts;
          var p = Math.min((ts - start) / 1200, 1);
          // نعدّل قيمة نفس الـ text node (مو textContent) عشان React ما يتأثر
          node.nodeValue = p < 1 ? Math.floor(target * p).toLocaleString('en-US') + '+' : original;
          if (p < 1) requestAnimationFrame(step);
        }
        requestAnimationFrame(step);
      }, { threshold: 0.4 });
      obs.observe(el);
    });
  }

  /* ============================================================
     5) خلفية الخطوط الطبوغرافية
     الـ canvas ينضاف بآخر الـ body وورا كل المحتوى (z-index: -1)
     ============================================================ */
  function initTopoBackground() {
    if (document.getElementById('rmz-topo-bg')) return;
    var canvas = document.createElement('canvas');
    canvas.id = 'rmz-topo-bg';
    canvas.setAttribute('aria-hidden', 'true');
    document.body.appendChild(canvas);

    var ctx = canvas.getContext('2d');
    if (!ctx) return;

    var LINE_RGB = '110,27,51';   // عنابي الشعار
    var LINE_COUNT = 34;          // عدد الخطوط (أكثر = أكثف)
    var LEVEL_RANGE = 1.8;
    var NOISE_SCALE = 0.0042;     // أكبر = تعرجات أكثر وأصغر
    var SPEED = prefersReduced ? 0 : 0.00009;
    var OPACITY_MIN = 0.12;
    var OPACITY_MAX = 0.32;
    var LINE_WIDTH = 1.2;
    var CELL = 8;
    var FRAME_MS = 1000 / 30;

    var W, H, cols, rows, field;

    function resize() {
      var dpr = Math.min(window.devicePixelRatio || 1, 2);
      W = window.innerWidth;
      H = window.innerHeight;
      canvas.width = W * dpr;
      canvas.height = H * dpr;
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      cols = Math.ceil(W / CELL) + 1;
      rows = Math.ceil(H / CELL) + 1;
      field = new Float32Array(cols * rows);
    }

    function noise(x, y, t) {
      return (
        Math.sin(x + t * 1.2) * Math.cos(y - t) +
        Math.sin((x + y) * 0.55 - t * 0.7) * 0.6 +
        Math.cos(x * 0.35 - y * 0.45 + t * 1.5) * 0.4
      );
    }

    function lerp(a, b, va, vb, level) {
      var d = vb - va;
      return a + (b - a) * (d === 0 ? 0.5 : (level - va) / d);
    }

    function seg(ax, ay, bx, by) {
      ctx.moveTo(ax, ay);
      ctx.lineTo(bx, by);
    }

    // Marching Squares: خطوط كونتور متصلة وناعمة
    function drawLevel(level) {
      for (var j = 0; j < rows - 1; j++) {
        var y0 = j * CELL, y1 = y0 + CELL;
        for (var i = 0; i < cols - 1; i++) {
          var x0 = i * CELL, x1 = x0 + CELL;
          var tl = field[j * cols + i], tr = field[j * cols + i + 1];
          var br = field[(j + 1) * cols + i + 1], bl = field[(j + 1) * cols + i];
          var code = (tl > level ? 8 : 0) | (tr > level ? 4 : 0) |
                     (br > level ? 2 : 0) | (bl > level ? 1 : 0);
          if (code === 0 || code === 15) continue;

          var tx = lerp(x0, x1, tl, tr, level), ry = lerp(y0, y1, tr, br, level);
          var bx = lerp(x0, x1, bl, br, level), ly = lerp(y0, y1, tl, bl, level);

          switch (code) {
            case 1: case 14: seg(x0, ly, bx, y1); break;
            case 2: case 13: seg(bx, y1, x1, ry); break;
            case 3: case 12: seg(x0, ly, x1, ry); break;
            case 4: case 11: seg(tx, y0, x1, ry); break;
            case 6: case 9:  seg(tx, y0, bx, y1); break;
            case 7: case 8:  seg(x0, ly, tx, y0); break;
            case 5: case 10:
              if (((tl + tr + br + bl) / 4 > level) === (tl > level)) {
                seg(tx, y0, x1, ry); seg(bx, y1, x0, ly);
              } else {
                seg(x0, ly, tx, y0); seg(bx, y1, x1, ry);
              }
          }
        }
      }
    }

    function drawFrame(time) {
      var t = time * SPEED;
      for (var j = 0; j < rows; j++) {
        for (var i = 0; i < cols; i++) {
          field[j * cols + i] = noise(i * CELL * NOISE_SCALE, j * CELL * NOISE_SCALE, t);
        }
      }
      ctx.clearRect(0, 0, W, H);
      ctx.lineWidth = LINE_WIDTH;
      for (var k = 0; k < LINE_COUNT; k++) {
        var level = ((k + 0.5) / LINE_COUNT * 2 - 1) * LEVEL_RANGE;
        var opacity = OPACITY_MIN + (OPACITY_MAX - OPACITY_MIN) * (1 - Math.abs(level) / LEVEL_RANGE);
        if (k % 5 === 0) opacity = Math.min(opacity + 0.12, 0.45); // كل خامس خط أغمق
        ctx.strokeStyle = 'rgba(' + LINE_RGB + ',' + opacity.toFixed(3) + ')';
        ctx.beginPath();
        drawLevel(level);
        ctx.stroke();
      }
    }

    var rafId = null, startTime = null, elapsed = 0, lastDraw = 0;

    function loop(ts) {
      if (startTime === null) startTime = ts - elapsed;
      elapsed = ts - startTime;
      if (ts - lastDraw >= FRAME_MS) {
        drawFrame(elapsed);
        lastDraw = ts;
      }
      rafId = requestAnimationFrame(loop);
    }

    var resizeTimeout;
    window.addEventListener('resize', function () {
      clearTimeout(resizeTimeout);
      resizeTimeout = setTimeout(function () { resize(); drawFrame(elapsed); }, 150);
    });

    document.addEventListener('visibilitychange', function () {
      if (document.hidden) {
        if (rafId) cancelAnimationFrame(rafId);
        rafId = null;
      } else if (SPEED > 0 && rafId === null) {
        startTime = null;
        rafId = requestAnimationFrame(loop);
      }
    });

    resize();
    if (SPEED > 0) rafId = requestAnimationFrame(loop);
    else drawFrame(0);
  }

  /* ====== التشغيل ====== */
  function safe(fn) {
    try { fn(); } catch (e) { if (window.console) console.warn('[theme]', e); }
  }

  // المتجر يتنقل بين الصفحات بدون إعادة تحميل، فنعيد تجهيز الأشياء
  // اللي تعتمد على الصفحة لما يتغير المحتوى (بتأخير بسيط لتجميع التغييرات)
  function watchPageChanges() {
    var pending = null;
    var lastPath = location.pathname;
    new MutationObserver(function () {
      if (pending) return;
      pending = setTimeout(function () {
        pending = null;
        safe(buildCatBar);
        if (location.pathname !== lastPath) {
          lastPath = location.pathname;
          safe(markActiveCat);
        }
        safe(animateCounters);
      }, 400);
    }).observe(document.body, { childList: true, subtree: true });
  }

  function start() {
    safe(initSplash);
    safe(initParallax);
    safe(initTopoBackground);
    safe(buildCatBar);
    safe(animateCounters);
    safe(watchPageChanges);
  }

  // ننتظر تحميل الصفحة كاملة عشان React يخلص بناء المتجر قبل ما نضيف شي
  if (document.readyState === 'complete') start();
  else window.addEventListener('load', start);
})();
