// Geohash Playground — Leaflet interop layer.
// All geometry comes pre-computed from .NET (the library is the star);
// this module only renders.

const MAX_LAT = 85.0511; // Web-Mercator visibility limit

let map = null;
let dotnet = null;
let canvasRenderer = null;
let svgRenderer = null;
let cursorChip = null;
let mode = 'explore';
let drawing = false;
let drawVerts = [];
let drawPreview = null;
let animToken = 0;

const layers = {}; // name -> L.LayerGroup

function group(name) {
    if (!layers[name]) {
        layers[name] = L.layerGroup().addTo(map);
    }
    return layers[name];
}

export function init(elId, dotnetRef) {
    dotnet = dotnetRef;

    map = L.map(elId, {
        center: [33, 12],
        zoom: 3,
        minZoom: 2,
        maxZoom: 19,
        zoomControl: false,
        worldCopyJump: false,
        maxBounds: [[-86.9, -1080], [86.9, 1080]],
        maxBoundsViscosity: 0.6,
        doubleClickZoom: true,
        attributionControl: true,
    });

    L.control.zoom({ position: 'bottomright' }).addTo(map);

    L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> &copy; <a href="https://carto.com/attributions">CARTO</a>',
        subdomains: 'abcd',
        maxZoom: 20,
    }).addTo(map);

    canvasRenderer = L.canvas({ padding: 0.3 });
    svgRenderer = L.svg({ padding: 0.3 });

    cursorChip = document.createElement('div');
    cursorChip.id = 'cursor-chip';
    document.body.appendChild(cursorChip);

    map.on('click', e => {
        if (drawing) { addVertex(e.latlng); return; }
        hideGhost();
        dotnet.invokeMethodAsync('OnMapClick', e.latlng.lat, e.latlng.lng);
    });

    map.on('dblclick', () => {
        if (drawing && drawVerts.length >= 3) finishDraw();
    });

    map.on('moveend zoomend', notifyView);
    map.on('zoomend', adjustSelectionFill);

    // Hover ghost: synchronous round-trip into .NET per animation frame.
    let pendingHover = null;
    map.on('mousemove', e => {
        if (pendingHover) return;
        pendingHover = requestAnimationFrame(() => {
            pendingHover = null;
            handleHover(e);
        });
    });

    map.on('mouseout', hideGhost);
    map.on('dragstart', hideGhost);
    document.documentElement.addEventListener('mouseleave', hideGhost);

    document.addEventListener('keydown', e => {
        if (e.key === 'Escape' && drawing) {
            cancelDraw();
            dotnet.invokeMethodAsync('OnDrawCancelled');
        }
    });

    notifyView();
    window.__dbg = { map, layers }; // debug hook, harmless in production
}

function notifyView() {
    if (!dotnet) return;
    const b = map.getBounds();
    dotnet.invokeMethodAsync('OnViewChanged',
        Math.max(b.getSouth(), -85.6), b.getWest(),
        Math.min(b.getNorth(), 85.6), b.getEast(),
        map.getZoom());
}

function handleHover(e) {
    if (drawing) { updateDrawPreview(e.latlng); hideGhostOnly(); return; }
    let info = null;
    try {
        // Synchronous JS -> .NET call: Geohasher.Encode under the cursor.
        info = dotnet.invokeMethod('OnHover', e.latlng.lat, e.latlng.lng);
    } catch { /* during prerender/teardown */ }

    if (!info) { hideGhost(); return; }

    const g = group('ghost');
    g.clearLayers();
    addRect(g, info.s, info.w, info.n, info.e, {
        renderer: svgRenderer,
        className: 'ghost-cell',
        color: '#22d3ee', weight: 1.2, opacity: 0.85,
        fillColor: '#22d3ee', fillOpacity: 0.07,
        interactive: false,
    });

    cursorChip.textContent = info.h;
    cursorChip.classList.add('show');
    cursorChip.style.left = e.originalEvent.clientX + 'px';
    cursorChip.style.top = e.originalEvent.clientY + 'px';
}

function hideGhostOnly() {
    if (layers['ghost']) layers['ghost'].clearLayers();
}

function hideGhost() {
    hideGhostOnly();
    if (cursorChip) cursorChip.classList.remove('show');
}

export function setMode(m) {
    mode = m;
    hideGhost();
    if (drawing) cancelDraw();
}

export function setGhostEnabled(on) {
    if (!on) hideGhost();
}

// ── Rectangles ──────────────────────────────────────────────────

function clampLat(lat) {
    return Math.max(-MAX_LAT, Math.min(MAX_LAT, lat));
}

// Adds a rectangle; near the date line also adds a wrapped twin so coverage
// looks seamless on every world copy.
function addRect(g, s, w, n, e, opts) {
    const make = (w2, e2) =>
        L.rectangle([[clampLat(s), w2], [clampLat(n), e2]], opts).addTo(g);

    const r = make(w, e);
    if (w < -150) make(w + 360, e + 360);
    if (e > 150) make(w - 360, e - 360);
    return r;
}

function addLabel(g, lat, lng, html, cls) {
    const icon = L.divIcon({ className: 'geo-label', html: `<span class="${cls || ''}">${html}</span>`, iconSize: [0, 0] });
    L.marker([clampLat(lat), lng], { icon, interactive: false, keyboard: false }).addTo(g);
    if (lng < -150) L.marker([clampLat(lat), lng + 360], { icon, interactive: false, keyboard: false }).addTo(g);
    if (lng > 150) L.marker([clampLat(lat), lng - 360], { icon, interactive: false, keyboard: false }).addTo(g);
}

// Batch cell rendering. bounds = flat [s,w,n,e,...] in hash-sorted order,
// so the staggered reveal sweeps along the Z-order curve.
export function drawCells(name, bounds, hashes, opts) {
    const g = group(name);
    if (!opts.append) g.clearLayers();

    const count = bounds.length / 4;
    const style = {
        renderer: canvasRenderer,
        color: opts.color || '#22d3ee',
        weight: opts.weight ?? 1,
        opacity: opts.opacity ?? 0.75,
        fillColor: opts.fillColor || opts.color || '#22d3ee',
        fillOpacity: opts.fillOpacity ?? 0.13,
        interactive: false,
    };

    let labels = opts.labels && hashes && count <= 1200;
    let labelClass = opts.labelClass || '';

    if (labels && count > 0) {
        // Label weight follows cell size on screen: big sparse cells get real
        // labels, dense grids get faint texture, specks get nothing.
        const a = map.latLngToContainerPoint([clampLat(bounds[0]), bounds[1]]);
        const b = map.latLngToContainerPoint([clampLat(bounds[2]), bounds[3]]);
        const px = Math.abs(b.x - a.x);
        if (px < 34) labels = false;
        else if (px < 95) labelClass = 'lo';
    }

    const token = ++animToken;

    const addOne = i => {
        const s = bounds[i * 4], w = bounds[i * 4 + 1], n = bounds[i * 4 + 2], e = bounds[i * 4 + 3];
        addRect(g, s, w, n, e, style);
        if (labels) addLabel(g, (s + n) / 2, (w + e) / 2, hashes[i], labelClass);
    };

    if (!opts.animate || count > 9000) {
        for (let i = 0; i < count; i++) addOne(i);
        return;
    }

    // Reveal across ~20 frames, in Z-curve order.
    const frames = 20;
    const perFrame = Math.max(1, Math.ceil(count / frames));
    let i = 0;
    const step = () => {
        if (token !== animToken) return; // superseded
        const end = Math.min(count, i + perFrame);
        for (; i < end; i++) addOne(i);
        if (i < count) requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
}

export function drawOutline(name, s, w, n, e, color, weight, dash, fillOpacity) {
    const g = group(name);
    g.clearLayers();
    addRect(g, s, w, n, e, {
        renderer: svgRenderer,
        color, weight, opacity: 0.95,
        dashArray: dash || null,
        fillColor: color, fillOpacity: fillOpacity ?? 0,
        interactive: false,
    });
}

let selBox = null;

export function drawSelection(s, w, n, e, zoomIfTiny) {
    const g = group('selection');
    g.clearLayers();
    selBox = [s, w, n, e];
    addRect(g, s, w, n, e, {
        renderer: svgRenderer,
        className: 'sel-cell',
        color: '#22d3ee', weight: 2.5, opacity: 1,
        fillColor: '#22d3ee', fillOpacity: 0.16,
        interactive: false,
    });
    adjustSelectionFill();

    if (zoomIfTiny) {
        // If the cell is a speck at the current zoom, fly partway in — to where
        // the cell is ~110 px and keeps its surroundings, not filling the screen.
        const p1 = map.latLngToContainerPoint([clampLat(s), w]);
        const p2 = map.latLngToContainerPoint([clampLat(n), e]);
        const px = Math.max(Math.abs(p2.x - p1.x), Math.abs(p2.y - p1.y));
        if (px < 14) {
            const dz = Math.log2(110 / Math.max(px, 0.5));
            const target = Math.min(Math.round(map.getZoom() + dz), 17);
            map.flyTo([(clampLat(s) + clampLat(n)) / 2, (w + e) / 2], target, { duration: 1.2 });
        }
    }
}

// When the user zooms inside their selected cell, a 16% fill would wash the
// whole viewport teal — fade it to a border-only highlight.
function adjustSelectionFill() {
    if (!selBox || !layers['selection']) return;
    const a = map.latLngToContainerPoint([clampLat(selBox[0]), selBox[1]]);
    const b = map.latLngToContainerPoint([clampLat(selBox[2]), selBox[3]]);
    const px = Math.min(Math.abs(b.x - a.x), Math.abs(b.y - a.y));
    const vp = Math.min(map.getSize().x, map.getSize().y);
    const fill = px > vp * 0.75 ? 0.03 : 0.16;
    layers['selection'].eachLayer(l => l.setStyle && l.setStyle({ fillOpacity: fill }));
}

export function drawNeighbors(items) {
    const g = group('neighbors');
    g.clearLayers();
    for (const it of items) {
        addRect(g, it.s, it.w, it.n, it.e, {
            renderer: svgRenderer,
            color: '#a78bfa', weight: 1.3, opacity: 0.8, dashArray: '4 4',
            fillColor: '#a78bfa', fillOpacity: 0.06,
            interactive: false,
        });
        addLabel(g, (it.s + it.n) / 2, (it.w + it.e) / 2,
            `${it.dir}<br>${it.h}`, 'dir');
    }
}

export function drawChildren(items) {
    const g = group('children');
    g.clearLayers();
    for (const it of items) {
        addRect(g, it.s, it.w, it.n, it.e, {
            renderer: svgRenderer,
            color: '#f472b6', weight: 1, opacity: 0.55,
            fillColor: '#f472b6', fillOpacity: 0.05,
            interactive: false,
        });
        addLabel(g, (it.s + it.n) / 2, (it.w + it.e) / 2,
            `${it.p}<b>${it.c}</b>`, 'child');
    }
}

export function drawZCurve(points) {
    const g = group('zcurve');
    g.clearLayers();
    const latlngs = [];
    for (let i = 0; i < points.length; i += 2) {
        latlngs.push([clampLat(points[i]), points[i + 1]]);
    }
    L.polyline(latlngs, {
        renderer: svgRenderer,
        className: 'zcurve',
        color: '#f472b6', weight: 2, opacity: 0.9,
        interactive: false,
    }).addTo(g);
}

export function drawCircle(lat, lng, radiusM) {
    const g = group('circle');
    g.clearLayers();

    const make = ln => {
        L.circle([lat, ln], {
            renderer: svgRenderer,
            radius: radiusM,
            color: '#ffffff', weight: 1.6, opacity: 0.7, dashArray: '6 6',
            fill: false, interactive: false,
        }).addTo(g);
        L.marker([lat, ln], {
            icon: L.divIcon({ className: 'geo-label', html: '<div class="pulse-marker"></div>', iconSize: [0, 0] }),
            interactive: false, keyboard: false,
        }).addTo(g);
    };

    make(lng);
    if (lng < -90) make(lng + 360);
    if (lng > 90) make(lng - 360);
}

export function drawPolygon(flatLatLngs) {
    const g = group('polygon');
    g.clearLayers();
    const latlngs = [];
    for (let i = 0; i < flatLatLngs.length; i += 2) {
        latlngs.push([flatLatLngs[i], flatLatLngs[i + 1]]);
    }
    L.polygon(latlngs, {
        renderer: svgRenderer,
        color: '#ffffff', weight: 1.6, opacity: 0.75, dashArray: '6 6',
        fillColor: '#ffffff', fillOpacity: 0.02,
        interactive: false,
    }).addTo(g);
}

export function clearLayer(name) {
    if (layers[name]) layers[name].clearLayers();
}

export function clearShapes() {
    for (const n of ['cells', 'selection', 'neighbors', 'children', 'zcurve', 'circle', 'polygon', 'ghost']) {
        clearLayer(n);
    }
}

export function flyTo(lat, lng, zoom) {
    map.flyTo([clampLat(lat), lng], zoom, { duration: 1.4 });
}

export function fitBounds(s, w, n, e, maxZoom) {
    map.flyToBounds([[clampLat(s), w], [clampLat(n), e]], { padding: [70, 70], duration: 1.2, maxZoom: maxZoom || 15 });
}

// ── Polygon drawing (hand-rolled, no plugin) ────────────────────

export function startDraw() {
    drawing = true;
    drawVerts = [];
    map.doubleClickZoom.disable();
    group('draw').clearLayers();
    map.getContainer().style.cursor = 'crosshair';
}

function addVertex(latlng) {
    drawVerts.push(latlng);
    const g = group('draw');
    L.marker(latlng, {
        icon: L.divIcon({ className: 'geo-label', html: '<div class="vertex-marker"></div>', iconSize: [0, 0] }),
        interactive: false, keyboard: false,
    }).addTo(g);
    redrawDrawLine(null);
    dotnet.invokeMethodAsync('OnDrawProgress', drawVerts.length);
}

function updateDrawPreview(latlng) {
    if (drawVerts.length === 0) return;
    redrawDrawLine(latlng);
}

function redrawDrawLine(previewPoint) {
    const g = group('draw');
    if (drawPreview) { g.removeLayer(drawPreview); drawPreview = null; }
    const pts = previewPoint ? [...drawVerts, previewPoint] : [...drawVerts];
    if (pts.length < 2) return;
    drawPreview = L.polyline(pts, {
        renderer: svgRenderer,
        color: '#f472b6', weight: 1.8, opacity: 0.9, dashArray: '5 5',
        interactive: false,
    }).addTo(g);
}

export function finishDraw() {
    if (!drawing) return;
    const verts = drawVerts;
    cancelDraw();
    if (verts.length < 3) return;
    const flat = [];
    for (const v of verts) { flat.push(v.lat, v.lng); }
    dotnet.invokeMethodAsync('OnPolygonDrawn', flat);
}

export function cancelDraw() {
    drawing = false;
    drawVerts = [];
    drawPreview = null;
    group('draw').clearLayers();
    map.doubleClickZoom.enable();
    map.getContainer().style.cursor = '';
}

// ── Small utilities ─────────────────────────────────────────────

export function setUrlHash(h) {
    history.replaceState(null, '', '#' + h);
}

export function getUrlHash() {
    return location.hash ? location.hash.substring(1) : '';
}

export async function copyText(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        const ta = document.createElement('textarea');
        ta.value = text;
        document.body.appendChild(ta);
        ta.select();
        const ok = document.execCommand('copy');
        ta.remove();
        return ok;
    }
}

export function dispose() {
    hideGhost();
    if (cursorChip) { cursorChip.remove(); cursorChip = null; }
    if (map) { map.remove(); map = null; }
    dotnet = null;
}
