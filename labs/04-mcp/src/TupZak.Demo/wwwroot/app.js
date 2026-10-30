// Tup Żak — prosty frontend demonstracji Tool Calling i MCP (bez frameworków).

const chatEl = document.getElementById('chat');
const traceEl = document.getElementById('trace');
const form = document.getElementById('chat-form');
const input = document.getElementById('chat-input');
const buttons = [...document.querySelectorAll('button')];
const resetButton = document.getElementById('reset');

let busy = false;

// ---------- pomocnicze ----------

const escapeHtml = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

function prettyJson(text) {
  let pretty;
  try { pretty = JSON.stringify(JSON.parse(text), compactArrays, 2); } catch { pretty = text ?? ''; }
  // Krótkie tablice współrzędnych w jednej linii, np. [0, 30].
  pretty = pretty.replace(/"__inline__(\[[^"]*\])"/g, '$1');
  return escapeHtml(pretty)
    .replace(/(&quot;[^&]*?&quot;)(\s*:)/g, '<span class="k">$1</span>$2')
    .replace(/:\s(&quot;.*?&quot;)/g, ': <span class="s">$1</span>')
    .replace(/(?<![\w"])(-?\d+(?:\.\d+)?)(?![\w"])/g, '<span class="n">$1</span>')
    .replace(/\b(true|false|null)\b/g, '<span class="b">$1</span>');
}

function compactArrays(key, value) {
  if (Array.isArray(value) && value.length <= 2 && value.every(v => typeof v === 'number'))
    return `__inline__[${value.join(', ')}]`;
  return value;
}

const seconds = ms => ms >= 1000 ? `${(ms / 1000).toFixed(1)} s` : `${ms.toFixed(1)} ms`;

function add(parent, html, className, tag = 'div') {
  const el = document.createElement(tag);
  if (className) el.className = className;
  el.innerHTML = html;
  parent.appendChild(el);
  parent.scrollTop = parent.scrollHeight;
  return el;
}

function setBusy(value) {
  busy = value;
  buttons.forEach(b => { if (b !== resetButton) b.disabled = value; });
  input.disabled = value;
  if (!value) input.focus();
}

// ---------- rozmowa ----------

function addChat(role, text) {
  const who = { user: 'Ty', agent: 'Tup Żak', error: 'Błąd', pending: 'Tup Żak' }[role];
  return add(chatEl, `<span class="who">${who}</span>${escapeHtml(text)}`, `msg ${role}`);
}

async function send(message) {
  if (busy || !message.trim()) return;
  setBusy(true);
  traceEl.querySelector('.empty')?.remove();

  addChat('user', message);
  add(traceEl, `Polecenie: „${escapeHtml(message)}”`, 'turn');
  const pending = addChat('pending', 'Agent pracuje…');

  try {
    const response = await fetch('/api/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message }),
    });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);

    // Odpowiedź to strumień NDJSON: każde zdarzenie Agent Loop w osobnej linii.
    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    for (;;) {
      const { value, done } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      let newline;
      while ((newline = buffer.indexOf('\n')) >= 0) {
        const line = buffer.slice(0, newline).trim();
        buffer = buffer.slice(newline + 1);
        if (line) handleEvent(JSON.parse(line), pending);
      }
    }
  } catch (err) {
    addChat('error', `Nie udało się wykonać polecenia: ${err.message}`);
  } finally {
    pending.remove();
    setBusy(false);
  }
}

// ---------- przebieg Tool Calling ----------

function handleEvent(e, pending) {
  switch (e.type) {
    case 'model': {
      const decision = e.toolCallCount > 0
        ? `→ model proponuje ${e.toolCallCount} ${e.toolCallCount === 1 ? 'wywołanie narzędzia' : 'wywołania narzędzi'}`
        : '→ odpowiedź końcowa (bez narzędzi)';
      const tokens = e.inputTokens != null ? ` · ${e.inputTokens} → ${e.outputTokens} tok.` : '';
      let html = `<div class="head"><span class="kind">MODEL</span><span>iteracja ${e.iteration}</span>
        <span class="meta">${seconds(e.durationMs)}${tokens}</span></div><p>${decision}</p>`;
      if (e.text && e.toolCallCount > 0) html += `<p>${escapeHtml(e.text)}</p>`;
      if (e.reasoning) html += `<details><summary>Rozumowanie modelu (thinking)</summary><div class="reasoning">${escapeHtml(e.reasoning)}</div></details>`;
      add(traceEl, html, 'step model');
      pending.innerHTML = `<span class="who">Tup Żak</span>Agent pracuje… (iteracja ${e.iteration})`;
      break;
    }
    case 'tool-call':
      add(traceEl, `<div class="head"><span class="kind">TOOL CALL →</span><span class="name">${escapeHtml(e.toolName)}</span>
        <span class="meta">id: ${escapeHtml(e.callId)}</span></div><pre>${prettyJson(e.arguments)}</pre>`, 'step call');
      break;
    case 'tool-result': {
      const error = e.error ? `<p class="error-text">Błąd: ${escapeHtml(e.error)}</p>` : '';
      add(traceEl, `<div class="head"><span class="kind">TOOL RESULT ←</span><span class="name">${escapeHtml(e.toolName)}</span>
        <span class="meta">${seconds(e.durationMs)} · id: ${escapeHtml(e.callId)}</span></div>${error}<pre>${prettyJson(e.result)}</pre>`,
        e.error ? 'step result failed' : 'step result');
      break;
    }
    case 'map':
      applyMapState(e.map);
      break;
    case 'final':
      addChat('agent', e.text || '(pusta odpowiedź modelu)');
      applyMapState(e.map);
      break;
    case 'error':
      add(traceEl, `<div class="head"><span class="kind">BŁĄD</span></div><p class="error-text">${escapeHtml(e.error)}</p>`, 'step failed');
      addChat('error', e.error);
      if (e.map) applyMapState(e.map);
      break;
  }
}

// ---------- mapa (Leaflet, lokalny układ współrzędnych w metrach) ----------

const map = L.map('map', { crs: L.CRS.Simple, minZoom: -3, maxZoom: 4, zoomSnap: 0.25, attributionControl: false, trackResize: false });
L.control.scale({ imperial: false, metric: true, position: 'bottomleft' }).addTo(map);

const buildingLayers = {};   // id -> { outline, group, entrances: { id -> latlng } }
let markedLayer = null;
let allBounds = null;

const styles = {
  building: { color: '#334155', weight: 2, fillColor: '#cbd5e1', fillOpacity: 0.9 },
  parking: { color: '#6b7280', weight: 2, dashArray: '6 4', fillColor: '#e5e7eb', fillOpacity: 0.8 },
  courtyard: { color: '#16a34a', weight: 2, fillColor: '#bbf7d0', fillOpacity: 0.8 },
  path: { color: '#a16207', weight: 3, dashArray: '6 6' },
};

function drawBuilding(building) {
  const entrances = {};
  let outline = null;

  const group = L.geoJSON(building.geoJson, {
    style: f => styles[f.properties.kind] ?? {},
    pointToLayer: (f, latlng) => {
      if (f.properties.kind === 'entrance') {
        entrances[f.properties.id] = latlng;
        return L.circleMarker(latlng, { radius: 8, color: '#1d4ed8', weight: 3, fillColor: '#fff', fillOpacity: 1 })
          .bindTooltip(f.properties.id, { permanent: true, direction: 'top', className: 'entrance-label', offset: [0, -6] });
      }
      return L.circleMarker(latlng, { radius: 6, color: '#7c3aed', weight: 2, fillColor: '#ddd6fe', fillOpacity: 1 })
        .bindTooltip(f.properties.name, { permanent: true, direction: 'right', className: 'label minor', offset: [6, 0] });
    },
    onEachFeature: (f, layer) => {
      const kind = f.properties.kind;
      if (kind === 'building') {
        outline = layer;
      } else if (kind === 'parking') {
        layer.bindTooltip('P', { permanent: true, direction: 'center', className: 'label' });
      } else if (kind === 'courtyard') {
        layer.bindTooltip(f.properties.name, { permanent: true, direction: 'center', className: 'label minor' });
      }
    },
  }).addTo(map);

  // Nazwa budynku nad jego obrysem, żeby nie zasłaniała wejść.
  const bounds = outline.getBounds();
  L.tooltip({ permanent: true, direction: 'top', className: 'label building-label', offset: [0, -18] })
    .setLatLng([bounds.getNorth(), bounds.getCenter().lng])
    .setContent(building.name)
    .addTo(map);

  // Wejścia zawsze nad wielokątami.
  group.eachLayer(l => { if (l instanceof L.CircleMarker) l.bringToFront(); });
  buildingLayers[building.id] = { group, outline, entrances };
}

let lastMapState = '{}';

function applyMapState(state) {
  if (!state) return;
  lastMapState = JSON.stringify(state);

  Object.entries(buildingLayers).forEach(([id, b]) =>
    b.outline?.setStyle(id === state.focusedBuildingId
      ? { color: '#1d4ed8', weight: 4 }
      : { color: styles.building.color, weight: styles.building.weight }));

  if (markedLayer) { map.removeLayer(markedLayer); markedLayer = null; }

  const building = buildingLayers[state.markedBuildingId];
  const latlng = building?.entrances[state.markedEntranceId];
  if (latlng) {
    markedLayer = L.marker(latlng, {
      icon: L.divIcon({ className: 'marked-entrance', html: '<div></div>', iconSize: [44, 44] }),
      zIndexOffset: 1000,
    }).bindTooltip(`${state.markedEntranceId} ✓`, { permanent: true, direction: 'bottom', className: 'marked-label', offset: [0, 26] })
      .addTo(map);
  }

  const focused = buildingLayers[state.focusedBuildingId];
  if (focused) map.flyToBounds(focused.group.getBounds(), { padding: [50, 50], duration: 0.6 });
  else if (allBounds) map.fitBounds(allBounds, { padding: [30, 30] });
}

async function loadMap() {
  const data = await (await fetch('/api/map')).json();
  data.buildings.forEach(drawBuilding);
  allBounds = L.latLngBounds([]);
  Object.values(buildingLayers).forEach(b => allBounds.extend(b.group.getBounds()));
  map.fitBounds(allBounds, { padding: [30, 30] });
  updateLabels();
  applyMapState(data.state);
}

async function loadInfo() {
  const info = await (await fetch('/api/info')).json();
  document.getElementById('model-info').textContent =
    `model: ${info.model} · limit: ${info.maxIterations} iteracji / ${info.timeoutSeconds} s`;
  document.getElementById('mcp-info').textContent = `MCP: ${location.origin}/mcp`;
}

// ---------- zdarzenia UI ----------

form.addEventListener('submit', e => {
  e.preventDefault();
  const message = input.value;
  input.value = '';
  send(message);
});

document.querySelectorAll('button.scenario').forEach(b =>
  b.addEventListener('click', () => send(b.dataset.prompt)));

resetButton.addEventListener('click', async () => {
  await fetch('/api/reset', { method: 'POST' });
  chatEl.innerHTML = '';
  traceEl.innerHTML = '<p class="empty">Tu pojawią się kolejne kroki Agent Loop oraz wywołania narzędzi przez klienta MCP (np. Claude Code).</p>';
  applyMapState({});
});

// ---------- MCP: wywołania narzędzi przez zewnętrznego hosta (np. Claude Code) ----------

// Turę prowadzi klient MCP, a nie przeglądarka, więc odpytujemy dziennik wywołań i stan mapy.
let mcpSequence = 0;
let mcpClient = null;

async function pollMcp() {
  try {
    const data = await (await fetch(`/api/mcp/activity?after=${mcpSequence}`)).json();
    if (data.lastSequence < mcpSequence) mcpSequence = 0;   // aplikacja została uruchomiona ponownie
    for (const item of data.items) {
      traceEl.querySelector('.empty')?.remove();
      if (item.client !== mcpClient) {
        mcpClient = item.client;
        add(traceEl, `MCP · klient: ${escapeHtml(item.client)}`, 'turn mcp');
      }
      handleEvent(item.event, null);
      mcpSequence = item.sequence;
    }
    if (!busy && JSON.stringify(data.map) !== lastMapState) applyMapState(data.map);
  } catch { /* aplikacja niedostępna — spróbujemy ponownie */ }
  setTimeout(pollMcp, 1000);
}

// ---------- układ paneli: przeciąganie rozdzielaczy i zwijanie ----------

const mainEl = document.querySelector('main');
const panels = [...document.querySelectorAll('.panel')];
const gutters = [...document.querySelectorAll('.gutter')];
const panelSizes = [27, 35, 38];   // udziały szerokości (fr)
const COLLAPSED_WIDTH = 46;
const MIN_PANEL_WIDTH = 200;

const isOpen = i => !panels[i].classList.contains('collapsed');

// Rozdzielacz zmienia szerokość najbliższych otwartych paneli po obu stronach.
function gutterPanels(gutterIndex) {
  const indexes = panels.map((_, i) => i).filter(isOpen);
  const left = indexes.findLast(i => i <= gutterIndex);
  const right = indexes.find(i => i > gutterIndex);
  return left === undefined || right === undefined ? null : [left, right];
}

function applyLayout() {
  mainEl.style.gridTemplateColumns = panels
    .map((_, i) => isOpen(i) ? `${panelSizes[i]}fr` : `${COLLAPSED_WIDTH}px`)
    .join(' 14px ');
  const openCount = panels.filter((_, i) => isOpen(i)).length;
  panels.forEach((panel, i) => {
    const toggle = panel.querySelector('.panel-toggle');
    toggle.textContent = isOpen(i) ? '−' : '+';
    toggle.title = isOpen(i) ? 'Ukryj panel' : 'Pokaż panel';
    toggle.disabled = isOpen(i) && openCount === 1;   // przynajmniej jeden panel zostaje otwarty
  });
  gutters.forEach((gutter, i) => gutter.classList.toggle('inactive', !gutterPanels(i)));
}

// Przyciski powstają tutaj, poza listą `buttons`, bo zwijanie działa także podczas pracy agenta.
panels.forEach(panel => {
  const title = panel.querySelector('h2');
  title.innerHTML = `<span>${title.innerHTML}</span>`;
  title.appendChild(Object.assign(document.createElement('button'), { type: 'button', className: 'panel-toggle' }));
  title.addEventListener('click', e => {
    if (e.target.closest('.panel-toggle') || panel.classList.contains('collapsed')) {
      panel.classList.toggle('collapsed');
      applyLayout();
    }
  });
});

gutters.forEach((gutter, gutterIndex) => {
  gutter.addEventListener('pointerdown', down => {
    const pair = gutterPanels(gutterIndex);
    if (!pair) return;
    const [left, right] = pair;
    down.preventDefault();
    gutter.setPointerCapture(down.pointerId);

    const startWidth = panels[left].offsetWidth;
    const totalWidth = startWidth + panels[right].offsetWidth;
    let dragged = startWidth;

    const clampedWidth = () => Math.min(Math.max(dragged, MIN_PANEL_WIDTH), totalWidth - MIN_PANEL_WIDTH);
    // Przeciągnięcie wyraźnie poza minimum zwija panel; po rozwinięciu wraca on do poprzedniej szerokości.
    const panelToHide = () => dragged < MIN_PANEL_WIDTH / 2 ? left
      : totalWidth - dragged < MIN_PANEL_WIDTH / 2 ? right
      : null;

    // Podczas przeciągania przesuwa się tylko linia podziału; panele zmieniają się po puszczeniu.
    const move = e => {
      dragged = startWidth + e.clientX - down.clientX;
      gutter.style.transform = `translateX(${clampedWidth() - startWidth}px)`;
      gutter.classList.toggle('collapsing', panelToHide() !== null);
    };
    gutter.classList.add('dragging');
    gutter.addEventListener('pointermove', move);

    gutter.addEventListener('lostpointercapture', () => {
      gutter.removeEventListener('pointermove', move);
      gutter.classList.remove('dragging', 'collapsing');
      gutter.style.transform = '';
      if (dragged === startWidth) return;

      const hidden = panelToHide();
      if (hidden !== null) {
        panels[hidden].classList.add('collapsed');
      } else {
        const totalSize = panelSizes[left] + panelSizes[right];
        panelSizes[left] = totalSize * clampedWidth() / totalWidth;
        panelSizes[right] = totalSize - panelSizes[left];
      }
      applyLayout();
    }, { once: true });
  });
});

applyLayout();

// Mapa potrzebuje rzeczywistych wymiarów kontenera po każdej zmianie układu.
// Zwiniętej mapy nie przeliczamy: Leaflet zachowuje ostatni rozmiar, więc flyToBounds działa także w tle.
const mapEl = document.getElementById('map');
new ResizeObserver(() => { if (mapEl.offsetWidth > 0) map.invalidateSize(); }).observe(mapEl);

// Przy małym przybliżeniu ukrywamy etykiety drugorzędne (dziedziniec, punkty orientacyjne).
const updateLabels = () => document.getElementById('map').classList.toggle('zoomed-out', map.getZoom() < 1.5);
map.on('zoomend', updateLabels);

loadInfo();
loadMap().then(pollMcp);
input.focus();
