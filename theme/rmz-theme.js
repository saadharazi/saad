(function () {
  function init() {
  var prefersReduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  var CONFIG = {
    logoUrl: '', // حط رابط شعارك الحقيقي هنا لو تبيه بشاشة الترحيب، وإلا بيستخدم اسم النص
    storeName: document.title || 'المتجر',
    splashDuration: 1600 // مدة ظهور شاشة الترحيب بالمللي ثانية
  };

  // sessionStorage ممكن يرمي خطأ بالتصفح الخفي أو إذا الكوكيز محظورة
  function sessionGet(key) {
    try { return sessionStorage.getItem(key); } catch (e) { return null; }
  }
  function sessionSet(key, value) {
    try { sessionStorage.setItem(key, value); } catch (e) {}
  }

  /* ====== 1) شاشة الترحيب — تظهر مرة وحدة كل جلسة ====== */
  if (!sessionGet('rmz_splash_shown')) {
    var splash = document.createElement('div');
    splash.id = 'rmz-splash';
    // نبني العناصر بدل innerHTML عشان عنوان الصفحة ما ينفّذ كـ HTML
    if (CONFIG.logoUrl) {
      var splashImg = document.createElement('img');
      splashImg.src = CONFIG.logoUrl;
      splashImg.alt = CONFIG.storeName;
      splashImg.style.maxWidth = '180px';
      splash.appendChild(splashImg);
    } else {
      var splashText = document.createElement('span');
      splashText.className = 'rmz-splash-text';
      splashText.textContent = CONFIG.storeName;
      splash.appendChild(splashText);
    }
    document.body.appendChild(splash);

    setTimeout(function () {
      splash.classList.add('rmz-hide');
      setTimeout(function () { splash.remove(); }, 650);
    }, prefersReduced ? 200 : CONFIG.splashDuration);

    sessionSet('rmz_splash_shown', '1');
  }

  /* ====== 2) عمق خفيف للبانر يتبع الماوس ====== */
  if (!prefersReduced) {
    var raf = null;
    document.addEventListener('mousemove', function (e) {
      if (raf) return;
      raf = requestAnimationFrame(function () {
        var px = (e.clientX / window.innerWidth - 0.5) * 10;
        var py = (e.clientY / window.innerHeight - 0.5) * 10;
        document.body.style.setProperty('--st-px', px.toFixed(2));
        document.body.style.setProperty('--st-py', py.toFixed(2));
        raf = null;
      });
    });
  }

  /* ====== 3) شريط كاتقوري من روابطك الحقيقية بالهيدر ====== */
  var catLinks = Array.from(document.querySelectorAll('a[href*="/category/"]'));
  var seen = {};
  var uniqueCats = catLinks.filter(function (a) {
    var href = a.getAttribute('href');
    if (seen[href]) return false;
    seen[href] = true;
    return true;
  });

  if (uniqueCats.length) {
    var bar = document.createElement('div');
    bar.className = 'rmz-cat-bar';

    var allLink = document.createElement('a');
    allLink.href = '/products';
    allLink.className = 'rmz-cat-link';
    allLink.textContent = 'جميع المنتجات';
    if (location.pathname === '/products') allLink.classList.add('rmz-active');
    bar.appendChild(allLink);

    uniqueCats.forEach(function (a) {
      var link = document.createElement('a');
      link.href = a.getAttribute('href');
      link.className = 'rmz-cat-link';
      link.textContent = a.textContent.trim();
      // نقارن بالمسار الكامل حتى لو الرابط الأصلي مطلق (https://...)
      if (location.pathname === link.pathname) link.classList.add('rmz-active');
      bar.appendChild(link);
    });

    var header = document.querySelector('header, .rmz-header, [class*="Header"]');
    if (header && header.parentNode) {
      header.parentNode.insertBefore(bar, header.nextSibling);
    }
  }

  /* ====== 4) نقل عداد الطلبات/العملاء الحقيقي لآخر الصفحة ====== */
  function findRealCounters() {
    var candidates = [];
    document.querySelectorAll('body *').forEach(function (el) {
      if (el.children.length === 0) {
        var t = el.textContent.trim();
        if (/^[\d,]+\+$/.test(t)) candidates.push(el);
      }
    });
    return candidates;
  }

  // أقرب حاوية مشتركة تضم كل العدادات (بدل اشتراط إن كلهم بنفس الـ div المباشر)
  function commonContainer(els) {
    var node = els[0].parentElement;
    while (node && node !== document.body) {
      var holdsAll = els.every(function (el) { return node.contains(el); });
      if (holdsAll && /^(SECTION|DIV)$/.test(node.tagName)) return node;
      node = node.parentElement;
    }
    return null;
  }

  var bottomSection = null;
  var counters = findRealCounters();
  var wrapper = counters.length ? commonContainer(counters) : null;

  if (wrapper) {
    bottomSection = document.createElement('div');
    bottomSection.className = 'rmz-bottom-section';
    var title = document.createElement('h3');
    title.textContent = 'أرقامنا الحقيقية';
    bottomSection.appendChild(title);
    bottomSection.appendChild(wrapper);

    var footer = document.querySelector('footer, [class*="Footer"]');
    if (footer && footer.parentNode) {
      footer.parentNode.insertBefore(bottomSection, footer);
    } else {
      document.body.appendChild(bottomSection);
    }

    // تحريك عد تصاعدي حقيقي عند الظهور (بدون تغيير الرقم الحقيقي)
    if (!prefersReduced && 'IntersectionObserver' in window) {
      counters.forEach(function (el) {
        var target = parseInt(el.textContent.replace(/[^\d]/g, ''), 10);
        if (!target) return;
        var suffix = el.textContent.trim().replace(/[\d,]/g, '');

        var obs = new IntersectionObserver(function (entries) {
          entries.forEach(function (entry) {
            if (!entry.isIntersecting) return;
            var start = null, duration = 1200;
            function step(ts) {
              if (!start) start = ts;
              var p = Math.min((ts - start) / duration, 1);
              el.textContent = Math.floor(target * p).toLocaleString('en-US') + suffix;
              if (p < 1) requestAnimationFrame(step);
              else el.textContent = target.toLocaleString('en-US') + suffix;
            }
            requestAnimationFrame(step);
            obs.unobserve(el);
          });
        }, { threshold: 0.4 });
        obs.observe(el);
      });
    }
  }

  /* ====== 5) رابط تقييمات العملاء الحقيقي — آخر الصفحة ====== */
  var reviewsLink = document.querySelector('a[href*="/reviews"]');
  if (reviewsLink) {
    // نعدّ بطاقات المراجعات فقط (مو كل عنوان h4/h5) عشان الرقم ما يطلع مضخّم
    var reviewsSection = reviewsLink.closest('section') || reviewsLink.parentElement;
    var reviewCount = 0;
    if (reviewsSection) {
      var cards = reviewsSection.querySelectorAll('[class*="review" i]');
      // نستبعد العناصر المتداخلة داخل بطاقة ثانية (عنوان البطاقة، نجومها...)
      reviewCount = Array.from(cards).filter(function (card) {
        var parent = card.parentElement && card.parentElement.closest('[class*="review" i]');
        return !parent || !reviewsSection.contains(parent) || parent === reviewsSection;
      }).length;
    }

    var badge = document.createElement('a');
    badge.href = '/reviews';
    badge.className = 'rmz-rating-badge';
    badge.textContent = reviewCount
      ? ('⭐ ' + reviewCount + ' تقييم من عملائنا — مشاهدة الكل')
      : 'مشاهدة تقييمات العملاء';

    (bottomSection || document.body).appendChild(badge);
  }

  /* ============================================================
     6) خلفية الموقع فقط — Topographic Lines بنفس ألوان الشعار
     ============================================================ */
  (function initTopoBackground() {
    var canvas = document.createElement('canvas');
    canvas.id = 'rmz-topo-bg';
    canvas.setAttribute('aria-hidden', 'true');
    document.body.insertBefore(canvas, document.body.firstChild);

    var ctx = canvas.getContext('2d');
    if (!ctx) return;
    var W, H, DPR;

    // نفس لون الشعار (--rmz-maroon-600) بدرجة وردية باهتة مطابقة للصورة المرسلة
    var LINE_RGB = '110,27,51';
    var LINE_COUNT = 34;        // عدد خطوط الكونتور (كل ما زاد صارت الخريطة أكثف)
    var LEVEL_RANGE = 1.8;      // مدى قيم الضوضاء اللي نرسم عليها خطوط
    var NOISE_SCALE = 0.0042;   // أكبر = تعرجات أكثر وأصغر
    var SPEED = prefersReduced ? 0 : 0.00009;
    var OPACITY_MIN = 0.12;
    var OPACITY_MAX = 0.32;
    var LINE_WIDTH = 1.2;
    var CELL = 8;           // حجم خلية الشبكة بالبكسل (الخطوط تبقى ناعمة بفضل الاستيفاء)
    var FRAME_MS = 1000 / 30; // الحركة بطيئة جداً، 30 إطار كافية وتوفر المعالج

    var cols, rows, field;

    function resize() {
      DPR = Math.min(window.devicePixelRatio || 1, 2);
      W = window.innerWidth;
      H = window.innerHeight;
      canvas.width = W * DPR;
      canvas.height = H * DPR;
      canvas.style.width = W + 'px';
      canvas.style.height = H + 'px';
      ctx.setTransform(DPR, 0, 0, DPR, 0, 0);

      cols = Math.ceil(W / CELL) + 1;
      rows = Math.ceil(H / CELL) + 1;
      field = new Float32Array(cols * rows);
    }

    function noise(x, y, t) {
      return (
        Math.sin(x * 1.0 + t * 1.2) * Math.cos(y * 1.0 - t) +
        Math.sin((x + y) * 0.55 - t * 0.7) * 0.6 +
        Math.cos(x * 0.35 - y * 0.45 + t * 1.5) * 0.4
      );
    }

    // نقطة تقاطع الخط مع ضلع الخلية (استيفاء خطي بين الزاويتين)
    function lerp(a, b, va, vb, level) {
      var d = vb - va;
      return a + (b - a) * (d === 0 ? 0.5 : (level - va) / d);
    }

    // Marching Squares: يرسم خطوط كونتور متصلة فعلية بدل خطوط عمودية متقطعة
    function drawLevel(level) {
      for (var j = 0; j < rows - 1; j++) {
        var y0 = j * CELL, y1 = y0 + CELL;
        for (var i = 0; i < cols - 1; i++) {
          var x0 = i * CELL, x1 = x0 + CELL;
          var tl = field[j * cols + i];
          var tr = field[j * cols + i + 1];
          var br = field[(j + 1) * cols + i + 1];
          var bl = field[(j + 1) * cols + i];

          var code = (tl > level ? 8 : 0) | (tr > level ? 4 : 0) |
                     (br > level ? 2 : 0) | (bl > level ? 1 : 0);
          if (code === 0 || code === 15) continue;

          // نقاط التقاطع على الأضلاع الأربعة: فوق، يمين، تحت، يسار
          var top = [lerp(x0, x1, tl, tr, level), y0];
          var right = [x1, lerp(y0, y1, tr, br, level)];
          var bottom = [lerp(x0, x1, bl, br, level), y1];
          var left = [x0, lerp(y0, y1, tl, bl, level)];

          switch (code) {
            case 1: case 14: seg(left, bottom); break;
            case 2: case 13: seg(bottom, right); break;
            case 3: case 12: seg(left, right); break;
            case 4: case 11: seg(top, right); break;
            case 6: case 9:  seg(top, bottom); break;
            case 7: case 8:  seg(left, top); break;
            case 5: case 10: // حالة السرج: نحسمها بقيمة مركز الخلية
              var center = (tl + tr + br + bl) / 4;
              if ((center > level) === (tl > level)) { seg(top, right); seg(bottom, left); }
              else { seg(left, top); seg(bottom, right); }
              break;
          }
        }
      }
    }

    function seg(a, b) {
      ctx.moveTo(a[0], a[1]);
      ctx.lineTo(b[0], b[1]);
    }

    function drawFrame(time) {
      ctx.clearRect(0, 0, W, H);
      var t = time * SPEED;

      // نحسب الضوضاء مرة وحدة للإطار ونعيد استخدامها لكل المستويات
      for (var j = 0; j < rows; j++) {
        for (var i = 0; i < cols; i++) {
          field[j * cols + i] = noise(i * CELL * NOISE_SCALE, j * CELL * NOISE_SCALE, t);
        }
      }

      ctx.lineWidth = LINE_WIDTH;
      ctx.lineCap = 'round';
      for (var k = 0; k < LINE_COUNT; k++) {
        var level = ((k + 0.5) / LINE_COUNT * 2 - 1) * LEVEL_RANGE;
        var opacity = OPACITY_MIN + (OPACITY_MAX - OPACITY_MIN) * (1 - Math.abs(level) / LEVEL_RANGE);
        // كل خامس خط أغمق شوي، مثل الخرائط الطبوغرافية الحقيقية
        if (k % 5 === 0) opacity = Math.min(opacity + 0.12, 0.45);
        ctx.strokeStyle = 'rgba(' + LINE_RGB + ',' + opacity.toFixed(3) + ')';
        ctx.beginPath();
        drawLevel(level);
        ctx.stroke();
      }
    }

    var rafId = null;
    var startTime = null;
    var elapsed = 0;     // الوقت المتراكم، عشان الحركة تكمل من مكانها بعد الرجوع للتبويب
    var lastDraw = 0;

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
      resizeTimeout = setTimeout(function () {
        resize();
        drawFrame(elapsed); // نرسم فوراً عشان ما تظهر الخلفية فاضية
      }, 150);
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
    if (SPEED > 0) {
      rafId = requestAnimationFrame(loop);
    } else {
      drawFrame(0);
    }
  })();
  }

  // لو السكربت انحط بالـ <head> يكون document.body لسا null، فننتظر تحميل الصفحة
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
