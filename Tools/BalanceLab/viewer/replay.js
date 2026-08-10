/*
 * 화면 3 — 리플레이 재생 + 튜닝 패널.
 * app.js가 만든 공용 도구를 window.BALANCE_VIEWER로 받아 쓴다 (파일 분리는 의존성이 아니라 크기 때문).
 */
(function () {
  'use strict';

  var V = window.BALANCE_VIEWER;
  var el = V.el;

  var SPEEDS = [0.25, 0.5, 1, 2, 4];

  /** 현재 화면의 스페이스바 핸들러 — 화면을 새로 그릴 때 반드시 교체한다 (누적 방지). */
  var spaceHandler = null;

  /**
   * 튜닝 가능한 필드 — 전부 열지 않는 이유: enum(이동 패턴·타겟팅)과 guid 참조는 슬라이더로 만질 물건이
   * 아니고, 리스트 필드는 애초에 오버라이드가 거절한다. 여기 없는 값은 .asset에서 고쳐야 한다.
   */
  var SOLDIER_FIELDS = [
    'maxHp', 'attackDamage', 'defense', 'critChancePercent',
    'attackInterval', 'attackRange', 'moveSpeed', 'unitRadius', 'projectileSpeed',
  ];
  var GENERAL_FIELDS = [
    'generalHpMultiplier', 'generalDamageMultiplier', 'generalChargeRequired',
    'generalActiveParamA', 'generalActiveParamB', 'generalActiveDuration',
  ];
  var CONFIG_FIELDS = [
    'defenseK', 'critMultiplier', 'separationStrength',
    'formationSpacingMultiplier', 'meleeSuppressDuration', 'meleeSuppressMagnitude',
  ];

  // ---------- 궤적 디코딩 ----------

  /** 프레임 = [생존수 u16][생존수 × (유닛 u16, x i16, y i16, hp u8)] · 전부 리틀엔디안. */
  function decodeFrames(base64) {
    var bin = atob(base64);
    var buf = new Uint8Array(bin.length);
    for (var i = 0; i < bin.length; i++) { buf[i] = bin.charCodeAt(i); }
    var dv = new DataView(buf.buffer);

    var frames = [];
    var o = 0;
    while (o + 2 <= buf.length) {
      var n = dv.getUint16(o, true); o += 2;
      var idx = new Uint16Array(n), xs = new Int16Array(n), ys = new Int16Array(n), hp = new Uint8Array(n);
      for (var k = 0; k < n; k++) {
        idx[k] = dv.getUint16(o, true);
        xs[k] = dv.getInt16(o + 2, true);
        ys[k] = dv.getInt16(o + 4, true);
        hp[k] = buf[o + 6];
        o += 7;
      }
      frames.push({ idx: idx, xs: xs, ys: ys, hp: hp });
    }
    return frames;
  }

  // ---------- 진입 ----------

  window.BALANCE_RENDER_REPLAY = function (route) {
    var entry = V.findEntry(route.name, route.tag);
    if (!entry) {
      V.show([V.missing('인덱스에 없는 시나리오다: ' + route.name, '전체 매트릭스를 다시 돌려라.',
        'dotnet run --project Tools/BalanceLab -- Tools/BalanceLab/scenarios/all.json')],
        [{ text: '매트릭스', hash: '#/' }]);
      return;
    }

    var stem = entry.tag ? entry.name + '.' + entry.tag : entry.name;
    var file = V.RESULTS + 'replay/' + stem + '.' + route.seed + '.js';
    var replayKey = entry.name + ':' + (entry.tag || '') + ':' + route.seed;

    Promise.all([V.loadScript(file), V.loadScript(V.RESULTS + 'assets.js')]).then(function () {
      var data = (window.BALANCE_REPLAY || {})[replayKey];
      if (!data) {
        V.show([V.missing('시드 ' + route.seed + '의 궤적 파일이 없다',
          '아래 명령을 돌리면 생성된다.', V.runCommand(entry, '--replay ' + route.seed))],
          [{ text: '매트릭스', hash: '#/' },
           { text: entry.name, hash: V.hashFor('s', entry.name, entry.tag) },
           { text: '시드 ' + route.seed }]);
        return;
      }
      draw(entry, data, route.seed);
    });
  };

  function draw(entry, data, seed) {
    var frames = decodeFrames(data.frames);
    if (!frames.length) {
      V.show([V.missing('궤적이 비어 있다', '리플레이 파일이 손상됐다. 다시 덤프하라.',
        V.runCommand(entry, '--replay ' + seed))], [{ text: '매트릭스', hash: '#/' }]);
      return;
    }

    var canvas = el('canvas', { width: '840', height: '560', 'aria-label': '전장 탑다운 재생' });
    var playBtn = el('button', { text: '재생', class: 'primary', style: 'width:74px' });
    var scrub = el('input', { type: 'range', min: '0', max: String(frames.length - 1), value: '0', step: '1' });
    var stepBack = el('button', { text: '◂', title: '한 프레임 뒤로' });
    var stepFwd = el('button', { text: '▸', title: '한 프레임 앞으로' });
    var speedSel = el('select', { style: 'width:78px' },
      SPEEDS.map(function (s) { return el('option', { value: String(s), text: s + 'x' }); }));
    speedSel.value = '1';
    var readout = el('span', { class: 'sub', style: 'min-width:132px;text-align:right' });
    var counts = el('div', { class: 'legend', style: 'margin-top:8px' });
    var eventLine = el('span', { class: 'sub' });

    var state = { frame: 0, playing: false, last: 0, acc: 0 };
    var frameSeconds = data.sampleEvery / data.tickRate;

    function tickOf(index) { return Math.min(data.ticks, index * data.sampleEvery); }

    function render() {
      var t = state.acc / frameSeconds;
      paint(canvas, data, frames, state.frame, Math.min(1, Math.max(0, t)));
      scrub.value = String(state.frame);
      readout.textContent = tickOf(state.frame) + ' / ' + data.ticks + '틱';
      updateCounts(counts, data, frames[state.frame]);
      eventLine.textContent = describeEvents(data, tickOf(state.frame));
    }

    function loop(now) {
      if (!state.playing) { return; }
      // 탭이 가려지면 브라우저가 rAF를 멈춘다 — 돌아왔을 때 밀린 시간이 한꺼번에 들어오면 전투가
      // 순간이동한다. 한 프레임에 반영할 시간을 잘라 그 점프를 막는다.
      var dt = state.last ? Math.min(0.25, (now - state.last) / 1000) : 0;
      state.last = now;
      state.acc += dt * parseFloat(speedSel.value);
      while (state.acc >= frameSeconds) {
        state.acc -= frameSeconds;
        if (state.frame >= frames.length - 1) {
          state.acc = 0;
          setPlaying(false);
          render();
          return;
        }
        state.frame++;
      }
      render();
      requestAnimationFrame(loop);
    }

    function setPlaying(on) {
      state.playing = on;
      playBtn.textContent = on ? '일시정지' : '재생';
      state.last = 0;
      if (on) {
        if (state.frame >= frames.length - 1) { state.frame = 0; }
        requestAnimationFrame(loop);
      }
    }

    playBtn.addEventListener('click', function () { setPlaying(!state.playing); });
    scrub.addEventListener('input', function () {
      setPlaying(false); state.frame = parseInt(scrub.value, 10); state.acc = 0; render();
    });
    stepBack.addEventListener('click', function () {
      setPlaying(false); state.frame = Math.max(0, state.frame - 1); state.acc = 0; render();
    });
    stepFwd.addEventListener('click', function () {
      setPlaying(false); state.frame = Math.min(frames.length - 1, state.frame + 1); state.acc = 0; render();
    });
    // 단축키는 document에 붙으므로 화면을 떠나도 남는다 — 리플레이를 여러 번 열면 핸들러가 쌓여
    // 스페이스 한 번에 여러 번 토글된다. 항상 직전 것을 떼고 새로 단다.
    if (spaceHandler) { document.removeEventListener('keydown', spaceHandler); }
    spaceHandler = function (e) {
      if (e.target.tagName === 'INPUT' || e.target.tagName === 'SELECT') { return; }
      if (e.code === 'Space') { e.preventDefault(); setPlaying(!state.playing); }
    };
    document.addEventListener('keydown', spaceHandler);

    var header = el('div', { class: 'bar' }, [
      el('h2', { text: entry.name + ' · 시드 ' + seed }),
      entry.tag ? el('span', { class: 'pill info', text: '실험 ' + entry.tag }) : null,
      el('span', {
        class: 'pill ' + (data.winner === 'left' ? 'info' : data.winner === 'right' ? 'warn' : ''),
        text: data.winner === 'left' ? '좌군 승' : data.winner === 'right' ? '우군 승' : '무승부',
      }),
      el('span', { class: 'spacer' }),
      el('span', {
        class: 'sub',
        text: data.ticks + '틱 · ' + frames.length + '프레임 (' + Math.round(data.tickRate / data.sampleEvery) + 'Hz 샘플)',
      }),
    ]);

    var stage = el('div', { class: 'card' }, [
      canvas,
      timeline(data),
      el('div', { class: 'ctrl' }, [playBtn, stepBack, scrub, stepFwd, readout, speedSel]),
      counts,
      el('div', { style: 'margin-top:6px' }, [eventLine]),
      el('div', { class: 'note', text: '스페이스바로 재생/정지. 색 = 진영, 큰 점 = 장군, 흐릿함 = 체력 낮음.' }),
    ]);

    V.show([header, el('div', { class: 'grid2' }, [stage, tuningPanel(entry, data)])],
      [{ text: '매트릭스', hash: '#/' },
       { text: entry.name, hash: V.hashFor('s', entry.name, entry.tag) },
       { text: '시드 ' + seed }]);

    render();
  }

  // ---------- 캔버스 ----------

  function paint(canvas, data, frames, index, alpha) {
    var ctx = canvas.getContext('2d');
    var W = canvas.width, H = canvas.height;
    var scale = data.posScale;
    var spanX = data.arenaHalfWidth * 2, spanY = data.arenaHalfHeight * 2;
    var k = Math.min(W / spanX, H / spanY);
    var ox = W / 2, oy = H / 2;

    ctx.clearRect(0, 0, W, H);
    ctx.strokeStyle = '#d8dade';
    ctx.setLineDash([5, 6]);
    ctx.beginPath(); ctx.moveTo(ox, 8); ctx.lineTo(ox, H - 8); ctx.stroke();
    ctx.setLineDash([]);
    ctx.strokeStyle = '#e6e8ea';
    ctx.strokeRect(ox - data.arenaHalfWidth * k, oy - data.arenaHalfHeight * k,
      spanX * k, spanY * k);

    var cur = frames[index];
    var next = frames[Math.min(index + 1, frames.length - 1)];
    // 다음 프레임에서 같은 유닛의 자리를 찾기 위한 역인덱스 — 프레임마다 생존 집합이 달라진다.
    var slot = {};
    for (var j = 0; j < next.idx.length; j++) { slot[next.idx[j]] = j; }

    for (var i = 0; i < cur.idx.length; i++) {
      var unit = cur.idx[i];
      var x = cur.xs[i] / scale, y = cur.ys[i] / scale;
      var nj = slot[unit];
      if (nj !== undefined && alpha > 0) {
        x += (next.xs[nj] / scale - x) * alpha;
        y += (next.ys[nj] / scale - y) * alpha;
      }
      var meta = data.units[unit];
      var px = ox + x * k;
      var py = oy - y * k; // 화면 y는 아래가 +
      var hp = cur.hp[i] / 255;

      ctx.globalAlpha = 0.35 + hp * 0.65;
      ctx.fillStyle = meta.team === 0 ? V.LEFT : V.RIGHT;
      ctx.beginPath();
      ctx.arc(px, py, meta.leader ? 7 : 3.4, 0, Math.PI * 2);
      ctx.fill();
      if (meta.leader) {
        ctx.globalAlpha = 1;
        ctx.strokeStyle = '#ffffff';
        ctx.lineWidth = 2;
        ctx.stroke();
      }
    }
    ctx.globalAlpha = 1;
  }

  function updateCounts(node, data, frame) {
    var left = 0, right = 0;
    for (var i = 0; i < frame.idx.length; i++) {
      if (data.units[frame.idx[i]].team === 0) { left++; } else { right++; }
    }
    node.innerHTML = '';
    node.appendChild(el('span', {}, [
      el('span', { class: 'dot', style: 'background:' + V.LEFT }), document.createTextNode('좌군 ' + left),
    ]));
    node.appendChild(el('span', {}, [
      el('span', { class: 'dot', style: 'background:' + V.RIGHT }), document.createTextNode('우군 ' + right),
    ]));
  }

  /** 액티브 발동과 분대 전멸만 마커로 세운다 — 사망은 수백 건이라 마커로 세우면 타임라인이 벽이 된다. */
  function timeline(data) {
    var strip = el('div', {
      style: 'position:relative;height:16px;margin-top:10px;border-bottom:1px solid var(--line)',
    });
    data.events.forEach(function (ev) {
      if (ev.type === 'death') { return; }
      var left = data.ticks ? (ev.tick / data.ticks) * 100 : 0;
      strip.appendChild(el('span', {
        title: (ev.type === 'activation' ? '장군 액티브' : '분대 전멸') + ' · ' + ev.tick + '틱',
        style: 'position:absolute;bottom:0;left:' + left + '%;width:2px;height:'
          + (ev.type === 'activation' ? '14px' : '8px') + ';background:'
          + (ev.team === 0 ? V.LEFT : V.RIGHT) + ';opacity:' + (ev.type === 'activation' ? '1' : '.45'),
      }));
    });
    return strip;
  }

  function describeEvents(data, tick) {
    var window_ = Math.max(2, Math.round(data.sampleEvery * 1.5));
    var hits = data.events.filter(function (e) {
      return e.type !== 'death' && Math.abs(e.tick - tick) <= window_;
    });
    var deaths = data.events.filter(function (e) {
      return e.type === 'death' && Math.abs(e.tick - tick) <= window_;
    }).length;

    var parts = hits.map(function (e) {
      return (e.team === 0 ? '좌' : '우') + ' ' + (e.type === 'activation' ? '장군 액티브 발동' : '분대 전멸');
    });
    if (deaths) { parts.push('사망 ' + deaths); }
    return parts.length ? '이벤트 · ' + parts.join(' / ') : '';
  }

  // ---------- 튜닝 패널 ----------

  function tuningPanel(entry, data) {
    var assets = window.BALANCE_ASSETS;
    if (!assets) {
      return el('div', { class: 'card' }, [
        el('div', { class: 'sect', text: '튜닝' }),
        el('div', { class: 'sub', text: 'results/assets.js가 없다. CLI를 다시 돌리면 생성된다.' }),
      ]);
    }

    var changes = {};          // { 에셋키: { 필드: 값 } } — 기준선과 다른 것만 남는다
    var body = el('div', {});
    var summary = el('div', { class: 'pill info', style: 'margin-bottom:10px', text: '변경 없음' });
    var output = el('div', {});

    function markChanged(assetKey, field, value, base) {
      if (Math.abs(value - base) < 1e-9) {
        if (changes[assetKey]) {
          delete changes[assetKey][field];
          if (!Object.keys(changes[assetKey]).length) { delete changes[assetKey]; }
        }
      } else {
        if (!changes[assetKey]) { changes[assetKey] = {}; }
        changes[assetKey][field] = value;
      }
      var count = Object.keys(changes).reduce(function (a, k) { return a + Object.keys(changes[k]).length; }, 0);
      summary.textContent = count ? count + '개 필드 변경됨' : '변경 없음';
      summary.className = 'pill ' + (count ? 'warn' : 'info');
      output.innerHTML = '';
    }

    // 이 판에 실제로 등장하는 롤만 연다 — 전 롤을 나열하면 패널이 관계없는 슬라이더로 뒤덮인다.
    var roles = [];
    (data.squads || []).forEach(function (s) { if (roles.indexOf(s.roleId) < 0) { roles.push(s.roleId); } });
    var hasGeneral = (data.squads || []).some(function (s) { return s.hasGeneral; });

    roles.forEach(function (role) {
      var base = assets.squads[role];
      if (!base) { return; }
      body.appendChild(el('div', { class: 'sect', style: 'margin-top:14px', text: '병사 · ' + role }));
      SOLDIER_FIELDS.forEach(function (f) {
        if (base[f] === undefined) { return; }
        body.appendChild(slider(role, f, base[f], markChanged));
      });
      if (hasGeneral) {
        body.appendChild(el('div', { class: 'sect', style: 'margin-top:14px', text: '장군 · ' + role }));
        GENERAL_FIELDS.forEach(function (f) {
          if (base[f] === undefined) { return; }
          body.appendChild(slider(role + 'General', f, base[f], markChanged));
        });
      }
    });

    body.appendChild(el('div', { class: 'sect', style: 'margin-top:14px', text: '전역 · BattleConfig' }));
    CONFIG_FIELDS.forEach(function (f) {
      if (assets.config[f] === undefined) { return; }
      body.appendChild(slider('config', f, assets.config[f], markChanged));
    });

    var nameInput = el('input', { type: 'text', value: 'exp1', style: 'width:100%' });
    var exportBtn = el('button', { class: 'primary', text: 'overrides.json 내보내기', style: 'width:100%' });
    exportBtn.addEventListener('click', function () {
      exportOverrides(entry, changes, nameInput.value.trim() || 'exp1', output);
    });

    return el('div', { class: 'card' }, [
      el('div', { class: 'sect', text: '튜닝 · 기준선은 .asset 원본' }),
      summary,
      body,
      el('div', { class: 'sect', style: 'margin-top:16px', text: '실험 이름 (--tag)' }),
      nameInput,
      el('div', { style: 'margin-top:8px' }, [exportBtn]),
      output,
    ]);
  }

  function slider(assetKey, field, base, onChange) {
    var value = base;
    var max = base > 0 ? base * 3 : 10;
    var step = pickStep(max);
    var readout = el('b', { text: format(value) });
    var wrap = el('div', { class: 'slider' });

    var range = el('input', {
      type: 'range', min: '0', max: String(round(max, step)), step: String(step), value: String(value),
    });
    var exact = el('input', { type: 'number', step: String(step), value: String(value), style: 'width:100%;margin-top:4px' });

    function apply(next, syncRange) {
      value = isFinite(next) ? next : base;
      readout.textContent = format(value);
      wrap.className = 'slider' + (Math.abs(value - base) < 1e-9 ? '' : ' dirty');
      if (syncRange) { range.value = String(Math.min(value, max)); }
      onChange(assetKey, field, value, base);
    }

    range.addEventListener('input', function () { exact.value = range.value; apply(parseFloat(range.value), false); });
    exact.addEventListener('change', function () { apply(parseFloat(exact.value), true); });

    wrap.appendChild(el('label', {}, [el('span', { text: field }), readout]));
    wrap.appendChild(range);
    wrap.appendChild(exact);
    return wrap;
  }

  function pickStep(max) {
    if (max >= 200) { return 1; }
    if (max >= 20) { return 0.5; }
    if (max >= 2) { return 0.05; }
    return 0.01;
  }

  function round(v, step) { return Math.round(v / step) * step; }

  function format(v) {
    return Math.abs(v - Math.round(v)) < 1e-9 ? String(Math.round(v)) : v.toFixed(2);
  }

  /**
   * 바뀐 필드만 모아 overrides.json을 만든다. 다운로드와 함께 실행 명령까지 붙여 주는 이유:
   * 파일만 받아도 어디에 어떻게 물리는지 모르면 루프가 닫히지 않는다.
   */
  function exportOverrides(entry, changes, tag, output) {
    output.innerHTML = '';
    var squads = {}, config = null;
    Object.keys(changes).forEach(function (k) {
      if (k === 'config') { config = changes[k]; } else { squads[k] = changes[k]; }
    });
    if (!Object.keys(squads).length && !config) {
      output.appendChild(el('div', { class: 'note', text: '바뀐 값이 없다 — 슬라이더를 먼저 움직여라.' }));
      return;
    }

    var payload = { note: entry.name + ' 실험 ' + tag };
    if (Object.keys(squads).length) { payload.squads = squads; }
    if (config) { payload.config = config; }
    var json = JSON.stringify(payload, null, 2);
    var path = 'Tools/BalanceLab/tuning/' + tag + '.json';
    var command = 'dotnet run --project Tools/BalanceLab -- ' + entry.scenarioPath
      + ' --overrides ' + path + ' --tag ' + tag;

    var dl = el('button', { text: tag + '.json 내려받기' });
    dl.addEventListener('click', function () {
      var blob = new Blob([json], { type: 'application/json' });
      var a = el('a', { href: URL.createObjectURL(blob), download: tag + '.json' });
      document.body.appendChild(a); a.click(); document.body.removeChild(a);
    });

    output.appendChild(el('div', { class: 'sect', style: 'margin-top:14px', text: path + ' 로 저장' }));
    output.appendChild(el('div', { class: 'cmd', text: json }));
    output.appendChild(el('div', { style: 'margin-top:8px' }, [dl]));
    output.appendChild(el('div', { class: 'sect', style: 'margin-top:14px', text: '저장 후 실행' }));
    output.appendChild(V.commandBlock(command));
  }
})();
