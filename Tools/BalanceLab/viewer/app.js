/*
 * BalanceLab 뷰어 — 의존성 0, 빌드 스텝 0. file:// 로 index.html을 더블클릭해도 그대로 돈다.
 *
 * 데이터는 전부 <script> 태그로 들어온다 (fetch는 file://에서 CORS로 막힌다):
 *   ../results/index.js                     → window.BALANCE_INDEX      (항상 선행 로드)
 *   ../results/<시나리오>[.<태그>].js        → window.BALANCE_RESULT[키]  (상세 진입 시 주입)
 *   ../results/replay/<시나리오>[.<태그>].<시드>.js → window.BALANCE_REPLAY[키] (재생 시 주입)
 * 키 규약은 전부 "<시나리오>:<태그 또는 빈문자>"(리플레이는 뒤에 ":<시드>")로 통일한다.
 */
(function () {
  'use strict';

  var RESULTS = '../results/';
  var LEFT = '#3b6fd4';
  var RIGHT = '#c2571f';
  var DRAW = '#8b9096';

  var app = document.getElementById('app');
  var crumbEl = document.getElementById('crumb');
  var stampEl = document.getElementById('stamp');

  // ---------- 작은 도구들 ----------

  function el(tag, attrs, kids) {
    var node = document.createElement(tag);
    if (attrs) {
      Object.keys(attrs).forEach(function (k) {
        if (k === 'text') { node.textContent = attrs[k]; }
        else if (k === 'html') { node.innerHTML = attrs[k]; }
        else if (k === 'on') { Object.keys(attrs.on).forEach(function (ev) { node.addEventListener(ev, attrs.on[ev]); }); }
        else if (k === 'style') { node.setAttribute('style', attrs[k]); }
        else { node.setAttribute(k, attrs[k]); }
      });
    }
    (kids || []).forEach(function (kid) { if (kid) { node.appendChild(kid); } });
    return node;
  }

  function pct(x) { return (x * 100).toFixed(1) + '%'; }
  function pct0(x) { return Math.round(x * 100) + '%'; }
  function key(name, tag) { return name + ':' + (tag || ''); }

  /** 태그 실행은 정본 결과와 파일명이 다르다 — 인덱스 항목 하나가 파일 하나에 대응한다. */
  function scriptName(name, tag) {
    return tag ? name + '.' + tag + '.js' : name + '.js';
  }

  var loading = {};
  /** 같은 파일을 두 번 주입하지 않는다. 실패해도 reject하지 않고 false를 돌려준다(없는 건 정상 상태다). */
  function loadScript(src) {
    if (loading[src]) { return loading[src]; }
    loading[src] = new Promise(function (resolve) {
      var s = document.createElement('script');
      s.src = src;
      s.onload = function () { resolve(true); };
      s.onerror = function () { resolve(false); };
      document.head.appendChild(s);
    });
    return loading[src];
  }

  function copy(text, button) {
    var done = function () {
      var was = button.textContent;
      button.textContent = '복사됨';
      setTimeout(function () { button.textContent = was; }, 1400);
    };
    // file:// 은 보안 컨텍스트가 아니라 clipboard API가 막힐 수 있다 — 구식 경로를 항상 남겨둔다.
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(done, function () { fallback(text, done); });
    } else {
      fallback(text, done);
    }
  }

  function fallback(text, done) {
    var ta = document.createElement('textarea');
    ta.value = text;
    ta.setAttribute('style', 'position:absolute;left:-9999px');
    document.body.appendChild(ta);
    ta.select();
    try { document.execCommand('copy'); done(); } catch (e) { /* 복사 실패는 조용히 둔다 — 명령문은 화면에 이미 보인다 */ }
    document.body.removeChild(ta);
  }

  function commandBlock(command, label) {
    var btn = el('button', { text: label || '명령 복사', on: { click: function () { copy(command, btn); } } });
    return el('div', {}, [
      el('div', { class: 'cmd', text: command }),
      el('div', { style: 'margin-top:8px' }, [btn]),
    ]);
  }

  /** 인덱스 항목으로 그 실행을 그대로 재현하는 명령을 만든다 (extra = 추가 인자). */
  function runCommand(entry, extra) {
    var c = 'dotnet run --project Tools/BalanceLab -- ' + (entry.scenarioPath || '<시나리오 경로>');
    if (entry.overridesPath) { c += ' --overrides ' + entry.overridesPath; }
    if (entry.tag) { c += ' --tag ' + entry.tag; }
    if (extra) { c += ' ' + extra; }
    return c;
  }

  function verdictPill(entry) {
    return el('span', {
      class: 'pill ' + (entry.passed ? 'ok' : 'bad'),
      text: entry.passed ? '통과' : '이탈',
    });
  }

  /**
   * 밴드가 실제로 판정한 값. 미러 매치업은 무승부가 정답이라 좌군 승률로는 통과할 수 없어
   * "승부 난 판의 좌군 비율"로 잰다(bands.json의 metric). 화면도 판정과 같은 값을 그려야
   * "0.0%인데 통과" 같은 모순이 안 생긴다. 구버전 결과에는 metricValue가 없으니 승률로 폴백한다.
   */
  function judged(entry) {
    return typeof entry.metricValue === 'number' ? entry.metricValue : entry.leftWinRate;
  }

  function metricLabel(entry) {
    return entry.metric === 'sideBalance' ? '승부 난 판의 좌군 비율' : '좌군 승률';
  }

  /** 목표 밴드를 배경 띠로, 실측을 세로선으로 — 숫자를 머릿속에서 비교하는 단계를 없앤다. */
  function bandTrack(entry, ghostRate) {
    var band = el('span', {
      class: 'band',
      style: 'left:' + (entry.bandMin * 100) + '%;width:' + ((entry.bandMax - entry.bandMin) * 100) + '%',
    });
    var kids = [band];
    if (typeof ghostRate === 'number') {
      kids.push(el('span', { class: 'ghost', style: 'left:calc(' + (ghostRate * 100) + '% - 1px)' }));
    }
    var value = judged(entry);
    kids.push(el('span', {
      class: 'mark',
      style: 'left:calc(' + (value * 100) + '% - 1px);background:' + (entry.passed ? 'var(--ok)' : 'var(--bad)'),
    }));
    kids.push(el('span', { class: 'val', text: pct(value) }));
    return el('span', {
      class: 'track',
      title: metricLabel(entry) + ' · 목표 ' + pct0(entry.bandMin) + '~' + pct0(entry.bandMax),
    }, kids);
  }

  // ---------- 라우팅 ----------

  function parseHash() {
    var raw = location.hash.replace(/^#\/?/, '');
    var parts = raw.split('/').map(decodeURIComponent);
    if (parts[0] === 's') { return { view: 's', name: parts[1], tag: parts[2] || null }; }
    if (parts[0] === 'r') { return { view: 'r', name: parts[1], tag: parts[2] || null, seed: parseInt(parts[3], 10) }; }
    return { view: 'm' };
  }

  function go(hash) { location.hash = hash; }

  function hashFor(view, name, tag, seed) {
    var h = '#/' + view + '/' + encodeURIComponent(name) + '/' + encodeURIComponent(tag || '');
    if (seed !== undefined && seed !== null) { h += '/' + seed; }
    return h;
  }

  function findEntry(name, tag) {
    var list = (window.BALANCE_INDEX && window.BALANCE_INDEX.scenarios) || [];
    for (var i = 0; i < list.length; i++) {
      if (list[i].name === name && (list[i].tag || null) === (tag || null)) { return list[i]; }
    }
    return null;
  }

  function show(nodes, crumbs) {
    app.innerHTML = '';
    nodes.forEach(function (n) { if (n) { app.appendChild(n); } });
    crumbEl.innerHTML = '';
    (crumbs || []).forEach(function (c, i) {
      if (i) { crumbEl.appendChild(document.createTextNode(' / ')); }
      if (c.hash) {
        crumbEl.appendChild(el('a', { href: c.hash, text: c.text }));
      } else {
        crumbEl.appendChild(document.createTextNode(c.text));
      }
    });
    window.scrollTo(0, 0);
  }

  // ---------- 화면 1: 매트릭스 ----------

  var filters = { band: '', onlyFailed: false, compare: '' };

  function renderMatrix() {
    var index = window.BALANCE_INDEX;
    var all = index.scenarios || [];
    var base = all.filter(function (e) { return !e.tag; });
    var tags = [];
    all.forEach(function (e) { if (e.tag && tags.indexOf(e.tag) < 0) { tags.push(e.tag); } });

    var bands = [];
    base.forEach(function (e) { if (bands.indexOf(e.band) < 0) { bands.push(e.band); } });

    var rows = base.filter(function (e) {
      if (filters.band && e.band !== filters.band) { return false; }
      if (filters.onlyFailed && e.passed) { return false; }
      return true;
    });

    var passed = base.filter(function (e) { return e.passed; }).length;
    var totalRuns = base.reduce(function (a, e) { return a + e.runs; }, 0);

    var cards = el('div', { class: 'cards' }, [
      metric('시나리오', String(base.length)),
      metric('밴드 통과', String(passed), passed === base.length ? 'var(--ok)' : null),
      metric('밴드 이탈', String(base.length - passed), base.length - passed ? 'var(--bad)' : null),
      metric('총 판수', totalRuns.toLocaleString()),
    ]);

    var bandSel = el('select', {}, [el('option', { value: '', text: '전체 밴드' })].concat(
      bands.map(function (b) { return el('option', { value: b, text: b }); })));
    bandSel.value = filters.band;
    bandSel.addEventListener('change', function () { filters.band = bandSel.value; renderMatrix(); });

    var failBtn = el('button', {
      text: filters.onlyFailed ? '전체 보기' : '이탈만 보기',
      class: filters.onlyFailed ? 'primary' : '',
      on: { click: function () { filters.onlyFailed = !filters.onlyFailed; renderMatrix(); } },
    });

    var bar = el('div', { class: 'bar' }, [bandSel, failBtn]);

    if (tags.length) {
      var cmpSel = el('select', {}, [el('option', { value: '', text: '비교 안 함' })].concat(
        tags.map(function (t) { return el('option', { value: t, text: '↔ ' + t }); })));
      cmpSel.value = filters.compare;
      cmpSel.addEventListener('change', function () { filters.compare = cmpSel.value; renderMatrix(); });
      bar.appendChild(el('span', { class: 'spacer' }));
      bar.appendChild(el('span', { class: 'sub', text: '실험 비교' }));
      bar.appendChild(cmpSel);
    }

    var head = el('tr', {}, [
      el('th', { text: '시나리오' }), el('th', { text: '밴드' }),
      el('th', { text: '판정 지표 / 목표 구간', style: 'width:38%' }),
      el('th', { text: filters.compare ? '실험 대비' : '판수' }),
      el('th', { text: '판정', style: 'text-align:right' }),
    ]);

    var body = el('tbody', {});
    rows.forEach(function (e) {
      var cmp = filters.compare ? findEntry(e.name, filters.compare) : null;
      var shown = cmp || e;
      var tr = el('tr', { on: { click: function () { go(hashFor('s', shown.name, shown.tag)); } } }, [
        el('td', {}, [
          el('div', { class: 'name', text: e.name }),
          cmp ? el('div', { class: 'sub', text: cmp.overrideNote || ('태그 ' + cmp.tag) }) : null,
        ]),
        el('td', {}, [el('span', { class: 'sub', text: e.band })]),
        el('td', {}, [bandTrack(shown, cmp ? judged(e) : undefined)]),
        el('td', {}, [cmp ? deltaNode(judged(e), judged(cmp)) : el('span', { class: 'sub', text: e.runs + '판' })]),
        el('td', { style: 'text-align:right' }, [verdictPill(shown)]),
      ]);
      body.appendChild(tr);
    });

    var table = el('table', {}, [el('thead', {}, [head]), body]);
    var card = el('div', { class: 'card' }, [bar, table,
      rows.length ? null : el('div', { class: 'empty', text: '조건에 맞는 시나리오가 없다' }),
      filters.compare ? el('div', { class: 'note', text: '회색 세로선 = 정본 승률, 색 세로선 = 실험 승률.' }) : null,
    ]);

    show([cards, card], [{ text: '매트릭스' }]);
  }

  function metric(k, v, color) {
    return el('div', { class: 'metric' }, [
      el('div', { class: 'k', text: k }),
      el('div', { class: 'v', text: v, style: color ? 'color:' + color : '' }),
    ]);
  }

  function deltaNode(from, to) {
    var d = (to - from) * 100;
    if (Math.abs(d) < 0.05) { return el('span', { class: 'sub', text: '변화 없음' }); }
    return el('span', {
      class: 'sub',
      style: 'color:' + (d > 0 ? 'var(--left)' : 'var(--right)'),
      text: (d > 0 ? '▲' : '▼') + Math.abs(d).toFixed(1) + 'p',
    });
  }

  // ---------- 화면 2: 시나리오 상세 ----------

  function renderDetail(route) {
    var entry = findEntry(route.name, route.tag);
    if (!entry) {
      show([missing('인덱스에 없는 시나리오다: ' + route.name + (route.tag ? ' [' + route.tag + ']' : ''),
        '전체 매트릭스를 다시 돌리면 인덱스가 갱신된다.',
        'dotnet run --project Tools/BalanceLab -- Tools/BalanceLab/scenarios/all.json')],
        [{ text: '매트릭스', hash: '#/' }, { text: route.name }]);
      return;
    }

    var k = key(entry.name, entry.tag);
    loadScript(RESULTS + scriptName(entry.name, entry.tag)).then(function () {
      var result = (window.BALANCE_RESULT || {})[k];
      if (!result) {
        show([missing('상세 결과 파일이 없다: ' + scriptName(entry.name, entry.tag),
          '이 시나리오를 다시 돌리면 생성된다.', runCommand(entry))],
          [{ text: '매트릭스', hash: '#/' }, { text: entry.name }]);
        return;
      }
      drawDetail(entry, result);
    });
  }

  function drawDetail(entry, result) {
    var stats = summarize(result);
    var banner = null;
    if (result.scenarioName !== entry.name || (result.tag || null) !== (entry.tag || null)) {
      banner = el('div', { class: 'banner', text: '경고: 인덱스 항목과 결과 파일이 어긋난다 (' + entry.name + ' vs ' + result.scenarioName + '). CLI를 다시 돌려라.' });
    }

    var header = el('div', { class: 'bar' }, [
      el('h2', { text: entry.name }),
      entry.tag ? el('span', { class: 'pill info', text: '실험 ' + entry.tag }) : null,
      verdictPill(entry),
      el('span', { class: 'spacer' }),
      el('span', { class: 'sub', text: '밴드 ' + entry.band + ' ' + pct0(entry.bandMin) + '~' + pct0(entry.bandMax) + ' · ' + metricLabel(entry) + ' ' + pct(judged(entry)) }),
    ]);

    var cards = el('div', { class: 'cards' }, [
      metric('좌군 승', entry.leftWins + '판', LEFT),
      metric('우군 승', entry.rightWins + '판', RIGHT),
      metric('무승부', entry.draws + '판'),
      metric('평균 종료 틱', String(Math.round(stats.meanTicks))),
    ]);

    var left = el('div', {}, [
      el('div', { class: 'sect', text: '종료 틱 분포 · ' + result.runs + '판 (' + stats.minTicks + '~' + stats.maxTicks + '틱)' }),
      histogram(stats),
      el('div', { class: 'sect', style: 'margin-top:22px', text: '시드별 승자 · 칸을 누르면 그 시드로 간다' }),
      el('div', { class: 'legend', style: 'margin-bottom:8px' }, [
        el('span', {}, [el('span', { class: 'dot', style: 'background:' + LEFT }), document.createTextNode('좌군 승')]),
        el('span', {}, [el('span', { class: 'dot', style: 'background:' + RIGHT }), document.createTextNode('우군 승')]),
        el('span', {}, [el('span', { class: 'dot', style: 'background:' + DRAW }), document.createTextNode('무승부')]),
        el('span', { class: 'sub', text: '· 흰 테두리 = 리플레이 있음' }),
      ]),
      seedGrid(entry, result),
    ]);

    var right = el('div', { class: 'card' }, [
      el('div', { class: 'sect', text: '분대 요약 · 평균' }),
      kv('좌 생존', stats.leftSurvivors.toFixed(1) + ' / ' + stats.leftTotal),
      kv('우 생존', stats.rightSurvivors.toFixed(1) + ' / ' + stats.rightTotal),
      stats.leftHasGeneral ? kv('좌 장군 생존', pct(stats.leftGeneralAlive)) : null,
      stats.rightHasGeneral ? kv('우 장군 생존', pct(stats.rightGeneralAlive)) : null,
      kv('좌 액티브 발동', stats.leftActivations.toFixed(2)),
      kv('우 액티브 발동', stats.rightActivations.toFixed(2)),
      el('div', { class: 'sect', style: 'margin-top:18px', text: '실행' }),
      kv('판수', String(result.runs)),
      kv('소요', result.elapsedMs + 'ms'),
      entry.overrideNote ? el('div', { class: 'note', text: '메모: ' + entry.overrideNote }) : null,
      el('div', { style: 'margin-top:14px' }, [commandBlock(runCommand(entry), '재현 명령 복사')]),
    ]);

    show([banner, header, cards, el('div', { class: 'grid2' }, [el('div', { class: 'card' }, [left]), right])],
      [{ text: '매트릭스', hash: '#/' }, { text: entry.name + (entry.tag ? ' [' + entry.tag + ']' : '') }]);
  }

  function kv(k, v) {
    return el('div', { class: 'kv' }, [el('span', { text: k }), el('span', { text: v })]);
  }

  function summarize(result) {
    var battles = result.battles;
    var ticks = battles.map(function (b) { return b.ticks; });
    var min = Math.min.apply(null, ticks);
    var max = Math.max.apply(null, ticks);
    var sum = ticks.reduce(function (a, b) { return a + b; }, 0);

    var acc = { l: 0, r: 0, lg: 0, rg: 0, la: 0, ra: 0, lgTotal: 0, rgTotal: 0 };
    battles.forEach(function (b) {
      b.leftSquads.forEach(function (s) {
        acc.l += s.survivors; acc.la += s.activations;
        if (s.hasGeneral) { acc.lgTotal++; if (s.generalAlive) { acc.lg++; } }
      });
      b.rightSquads.forEach(function (s) {
        acc.r += s.survivors; acc.ra += s.activations;
        if (s.hasGeneral) { acc.rgTotal++; if (s.generalAlive) { acc.rg++; } }
      });
    });

    var n = battles.length;
    return {
      ticks: ticks, minTicks: min, maxTicks: max, meanTicks: sum / n,
      leftSurvivors: acc.l / n, rightSurvivors: acc.r / n,
      leftTotal: totalSoldiers(result, 'left'), rightTotal: totalSoldiers(result, 'right'),
      leftHasGeneral: acc.lgTotal > 0, rightHasGeneral: acc.rgTotal > 0,
      leftGeneralAlive: acc.lgTotal ? acc.lg / acc.lgTotal : 0,
      rightGeneralAlive: acc.rgTotal ? acc.rg / acc.rgTotal : 0,
      leftActivations: acc.la / n, rightActivations: acc.ra / n,
    };
  }

  /** 초기 병력 수는 첫 판의 기록에서 읽는다 (모든 판이 같은 편성이다). */
  function totalSoldiers(result, side) {
    var squads = side === 'left' ? result.battles[0].leftSquads : result.battles[0].rightSquads;
    return squads.reduce(function (a, q) { return a + (q.soldiers || 0); }, 0);
  }

  function histogram(stats) {
    var BINS = 24;
    var lo = stats.minTicks;
    var hi = Math.max(stats.maxTicks, lo + 1);
    var counts = new Array(BINS).fill(0);
    stats.ticks.forEach(function (t) {
      var i = Math.min(BINS - 1, Math.floor((t - lo) / (hi - lo) * BINS));
      counts[i]++;
    });
    var peak = Math.max.apply(null, counts) || 1;

    var W = 700, H = 120, PAD = 18;
    var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('viewBox', '0 0 ' + W + ' ' + (H + PAD));
    svg.setAttribute('style', 'width:100%;height:auto');
    svg.setAttribute('role', 'img');
    svg.innerHTML = '<title>종료 틱 분포</title><desc>' + stats.minTicks + '틱부터 ' + stats.maxTicks + '틱까지 ' + BINS + '구간 히스토그램</desc>';

    counts.forEach(function (c, i) {
      var w = W / BINS;
      var h = c / peak * H;
      var r = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
      r.setAttribute('x', (i * w + 1).toFixed(1));
      r.setAttribute('y', (H - h).toFixed(1));
      r.setAttribute('width', (w - 2).toFixed(1));
      r.setAttribute('height', Math.max(h, 0.5).toFixed(1));
      r.setAttribute('rx', '2');
      r.setAttribute('fill', 'var(--accent)');
      r.setAttribute('opacity', '0.75');
      var t = document.createElementNS('http://www.w3.org/2000/svg', 'title');
      t.textContent = Math.round(lo + (hi - lo) * i / BINS) + '~' + Math.round(lo + (hi - lo) * (i + 1) / BINS) + '틱: ' + c + '판';
      r.appendChild(t);
      svg.appendChild(r);
    });

    [[0, stats.minTicks + '틱', 'start'], [W, stats.maxTicks + '틱', 'end']].forEach(function (a) {
      var t = document.createElementNS('http://www.w3.org/2000/svg', 'text');
      t.setAttribute('x', a[0]); t.setAttribute('y', H + 14);
      t.setAttribute('font-size', '11'); t.setAttribute('fill', 'var(--ink-3)');
      t.setAttribute('text-anchor', a[2]);
      t.textContent = a[1];
      svg.appendChild(t);
    });
    return svg;
  }

  // ---------- 시드 그리드 + 시드 직접 입력 ----------

  function seedColor(winner) {
    return winner === 'left' ? LEFT : winner === 'right' ? RIGHT : DRAW;
  }

  function seedGrid(entry, result) {
    var replaySeeds = entry.replaySeeds || [];
    var grid = el('div', { class: 'seeds' });
    result.battles.forEach(function (b) {
      var has = replaySeeds.indexOf(b.seed) >= 0;
      grid.appendChild(el('button', {
        class: 'seed' + (has ? ' has' : ''),
        style: 'background:' + seedColor(b.winner),
        title: '시드 ' + b.seed + ' · ' + b.winner + ' · ' + b.ticks + '틱' + (has ? ' · 리플레이 있음' : ''),
        'aria-label': '시드 ' + b.seed,
        on: { click: function () { openSeed(entry, result, b.seed); } },
      }));
    });

    var input = el('input', { type: 'number', min: '0', placeholder: '시드 번호', style: 'width:120px' });
    var goBtn = el('button', { text: '이 시드 보기', class: 'primary' });
    var out = el('div', { style: 'margin-top:12px' });

    function submit() {
      var raw = input.value.trim();
      if (raw === '') { return; }
      var n = parseInt(raw, 10);
      if (!isFinite(n) || n < 0 || String(n) !== raw.replace(/^0+(?=\d)/, '')) {
        out.innerHTML = '';
        out.appendChild(el('div', { class: 'banner', text: '시드는 0 이상의 정수여야 한다.' }));
        return;
      }
      openSeed(entry, result, n, out);
    }
    goBtn.addEventListener('click', submit);
    input.addEventListener('keydown', function (e) { if (e.key === 'Enter') { submit(); } });

    seedOut = out;
    return el('div', {}, [
      grid,
      el('div', { class: 'bar', style: 'margin-top:14px' }, [
        el('span', { class: 'sub', text: '시드 직접 입력' }), input, goBtn,
        el('span', { class: 'sub', text: '0 ~ ' + (result.runs - 1) }),
      ]),
      out,
    ]);
  }

  var seedOut = null;

  /**
   * 시드 하나를 여는 세 갈래.
   *   ① 리플레이가 덤프된 시드      → 재생 화면으로
   *   ② runs 안이지만 미덤프         → 기록을 펼치고 덤프 명령을 준다 ("없음"이 아니라 "한 줄이면 생김")
   *   ③ runs 밖                      → 범위를 알리고 넓혀 재실행하는 명령을 준다
   */
  function openSeed(entry, result, seed, outNode) {
    var out = outNode || seedOut;
    if (!out) { return; }
    out.innerHTML = '';

    if ((entry.replaySeeds || []).indexOf(seed) >= 0) {
      go(hashFor('r', entry.name, entry.tag, seed));
      return;
    }

    if (seed >= result.runs) {
      out.appendChild(el('div', { class: 'card' }, [
        el('div', { text: '시드 ' + seed + '는 이 실행 범위 밖이다 — 이 시나리오는 0 ~ ' + (result.runs - 1) + '까지 돌았다.' }),
        el('div', { class: 'note', text: '시나리오 JSON의 runs를 ' + (seed + 1) + ' 이상으로 올린 뒤 다시 돌려라 (' + entry.scenarioPath + ').' }),
        commandBlock(runCommand(entry, '--replay ' + seed)),
      ]));
      return;
    }

    var record = result.battles[seed];
    var rows = [
      kv('승자', record.winner === 'left' ? '좌군' : record.winner === 'right' ? '우군' : '무승부'),
      kv('종료 틱', String(record.ticks)),
    ];
    record.leftSquads.forEach(function (s) {
      rows.push(kv('좌 ' + s.squadId, s.survivors + ' / ' + s.soldiers + '명 생존' + (s.hasGeneral ? (s.generalAlive ? ' · 장군 생존' : ' · 장군 전사') : '') + ' · 발동 ' + s.activations));
    });
    record.rightSquads.forEach(function (s) {
      rows.push(kv('우 ' + s.squadId, s.survivors + ' / ' + s.soldiers + '명 생존' + (s.hasGeneral ? (s.generalAlive ? ' · 장군 생존' : ' · 장군 전사') : '') + ' · 발동 ' + s.activations));
    });

    out.appendChild(el('div', { class: 'card' }, [
      el('div', { class: 'bar' }, [
        el('h3', { text: '시드 ' + seed }),
        el('span', { class: 'pill warn', text: '리플레이 없음' }),
      ]),
      el('div', {}, rows),
      el('div', { class: 'note', text: '이 시드의 궤적은 아직 덤프되지 않았다. 아래 명령을 돌리면 재생할 수 있다.' }),
      commandBlock(runCommand(entry, '--replay ' + seed), '덤프 명령 복사'),
    ]));
    out.scrollIntoView({ block: 'nearest' });
  }

  // ---------- 빈 상태 ----------

  function missing(title, hint, command) {
    return el('div', { class: 'empty' }, [
      el('h2', { text: title }),
      el('div', { text: hint }),
      command ? el('div', { style: 'max-width:760px;margin:18px auto 0;text-align:left' }, [commandBlock(command)]) : null,
    ]);
  }

  // ---------- 진입 ----------

  function route() {
    if (!window.BALANCE_INDEX || !window.BALANCE_INDEX.scenarios || !window.BALANCE_INDEX.scenarios.length) {
      show([missing('결과 인덱스가 없다',
        'CLI를 먼저 돌려야 results/index.js가 만들어진다. 아래 명령이 전체 매트릭스를 돌린다.',
        'dotnet run --project Tools/BalanceLab -- Tools/BalanceLab/scenarios/all.json')], [{ text: '매트릭스' }]);
      return;
    }
    stampEl.textContent = '생성 ' + (window.BALANCE_INDEX.generatedAt || '?');

    var r = parseHash();
    if (r.view === 's') { renderDetail(r); }
    else if (r.view === 'r') { window.BALANCE_RENDER_REPLAY(r); }
    else { renderMatrix(); }
  }

  // 리플레이 화면은 replay.js가 채운다 — 없으면 안내만 띄운다(5단계 미적용 상태에서도 나머지가 돈다).
  window.BALANCE_RENDER_REPLAY = function (r) {
    show([missing('리플레이 화면이 없다', 'viewer/replay.js가 로드되지 않았다.')],
      [{ text: '매트릭스', hash: '#/' }, { text: r.name }]);
  };
  window.BALANCE_VIEWER = {
    el: el, show: show, missing: missing, kv: kv, metric: metric, key: key, pct: pct,
    findEntry: findEntry, loadScript: loadScript, commandBlock: commandBlock, runCommand: runCommand,
    hashFor: hashFor, RESULTS: RESULTS, LEFT: LEFT, RIGHT: RIGHT, scriptName: scriptName,
  };

  window.addEventListener('hashchange', route);
  // 첫 라우팅은 다음 태스크로 미룬다 — replay.js가 BALANCE_RENDER_REPLAY를 덮어쓸 틈을 줘야
  // 리플레이 URL로 바로 들어와도 안내문이 아니라 재생 화면이 뜬다.
  setTimeout(route, 0);
})();
