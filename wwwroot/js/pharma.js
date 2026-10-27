/* PharmaStock · núcleo de UI: estado, alertas, navegación por rol, utilidades */
const PS = (() => {
  const ALL = ['Administrador', 'Supervisor', 'Farmaceutico', 'Vendedor'];
  const NAV = [ // [pagina, texto, icono, roles permitidos]
    ['Dashboard', 'Panel principal', 'dash', ['Administrador', 'Supervisor', 'Farmaceutico']],
    ['Facturacion', 'Punto de venta', 'cart', ALL],
    ['Catalogo', 'Catálogo / Búsqueda', 'pill', ['Administrador', 'Supervisor', 'Farmaceutico']],
    ['Lotes', 'Lotes e ingresos', 'box', ['Administrador', 'Supervisor']],
    ['Movimientos', 'Ajustes y mermas', 'swap', ['Administrador', 'Supervisor']],
    ['Reportes', 'Reportes', 'chart', ['Administrador', 'Supervisor']]];
  const ICONS = {
    bell: '<path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9M13.73 21a2 2 0 0 1-3.46 0"/>',
    dash: '<rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/>',
    pill: '<rect x="2" y="8.5" width="20" height="7" rx="3.5" transform="rotate(-45 12 12)"/><path d="M8.5 8.5l7 7"/>',
    box: '<path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16zM3.27 6.96L12 12.01l8.73-5.05M12 22.08V12"/>',
    cart: '<circle cx="9" cy="21" r="1"/><circle cx="20" cy="21" r="1"/><path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"/>',
    swap: '<path d="M17 1l4 4-4 4M3 11V9a4 4 0 0 1 4-4h14M7 23l-4-4 4-4M21 13v2a4 4 0 0 1-4 4H3"/>',
    chart: '<path d="M18 20V10M12 20V4M6 20v-6"/>',
    pencil: '<path d="M12 20h9M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4z"/>',
    trash: '<path d="M3 6h18M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6M10 11v6M14 11v6M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/>',
    print: '<path d="M6 9V2h12v7M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2M6 14h12v8H6z"/>',
    eye: '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>',
    alert: '<path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0zM12 9v4M12 17h.01"/>',
    clock: '<circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/>',
    logout: '<path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9"/>',
    pdf: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6M16 13H8M16 17H8"/>',
    menu: '<path d="M3 12h18M3 6h18M3 18h18"/>', check: '<path d="M20 6L9 17l-5-5"/>', plus: '<path d="M12 5v14M5 12h14"/>',
    search: '<circle cx="11" cy="11" r="8"/><path d="M21 21l-4.35-4.35"/>', x: '<path d="M18 6L6 18M6 6l12 12"/>',
    shield: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>', trend: '<path d="M23 6l-9.5 9.5-5-5L1 18M17 6h6v6"/>'};
  const ico = n => `<svg class="ic"><use href="#i-${n}"/></svg>`;
  const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const money = n => '$' + Number(n || 0).toFixed(2);
  const midnight = (d = new Date()) => { d = new Date(d); d.setHours(0, 0, 0, 0); return d; };
  const iso = d => { d = new Date(d); return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'); };
  const daysTo = exp => Math.round((midnight(exp + 'T00:00:00') - midnight()) / 864e5);
  const parseMov = s => { const m = /(\d+)\/(\d+)\/(\d+) (\d+):(\d+)/.exec(s || ''); return m ? new Date(+m[3], m[2] - 1, +m[1], +m[4], +m[5]) : new Date(0); };
  const fmtDate = s => { const d = new Date(s.length === 10 ? s + 'T00:00:00' : s); return d.toLocaleDateString('es-SV', { day: '2-digit', month: 'short', year: 'numeric' }); };

  const cfg = { url: p => '/' + p, fetch: (u, o) => fetch(u, o) };
  let S = null, user = null, page = '';

  /* ---- Estado y derivados ---- */
  const lotState = l => {
    const d = daysTo(l.exp);
    if (d < 0) return { k: 'exp', cls: 'b-crit', txt: 'Vencido', d };
    if (d <= 30) return { k: 'soon', cls: 'b-warn', txt: 'Próximo a vencer', d };
    return { k: 'ok', cls: 'b-ok', txt: 'Vigente', d };
  };
  const derive = () => {
    const med = id => S.meds.find(m => m.id === id);
    S.med = med;
    S.lots.forEach(l => { l.m = med(l.med); l.st = lotState(l); });
    S.meds.forEach(m => { m.lots = S.lots.filter(l => l.med === m.id).sort((a, b) => a.exp.localeCompare(b.exp)); m.valid = m.lots.filter(l => l.st.k !== 'exp' && l.cur > 0); m.validStock = m.valid.reduce((s, l) => s + l.cur, 0); });
    S.low = S.meds.filter(m => m.on && m.stock <= m.min);
    S.soon = S.lots.filter(l => l.cur > 0 && l.st.k === 'soon').sort((a, b) => a.st.d - b.st.d);
    S.expired = S.lots.filter(l => l.cur > 0 && l.st.k === 'exp');
    S.sales.forEach(v => v.date = new Date(v.fecha));
    S.movs.forEach(m => m.date = parseMov(m.d));
  };
  const load = async () => { const r = await cfg.fetch('/api/state', { credentials: 'same-origin' }); if (r.status === 401) { location.href = '/Login'; return; } const j = await r.json(); S = S ? Object.assign(S, j) : j; derive(); renderBell(); return S; };

  /* ---- Mensajes ---- */
  const toast = (icon, title) => Swal.mixin({ toast: true, position: 'top-end', showConfirmButton: false, timer: 3800, timerProgressBar: true }).fire({ icon, title });
  const alertBox = (title, text, icon = 'error') => Swal.fire({ icon, title, text, confirmButtonColor: '#1e3a8a', confirmButtonText: 'Entendido' });
  const api = async (url, body) => {
    try {
      const r = await cfg.fetch(url, { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body || {}) });
      const j = await r.json().catch(() => ({}));
      if (r.status === 401) { location.href = '/Login'; return { ok: false, data: j }; }
      if (r.status === 403) alertBox('Acceso denegado', j.error || 'No tiene permisos para esta acción.', 'warning');
      return { ok: r.ok, data: j };
    } catch (e) { alertBox('Sin conexión', 'No se pudo contactar al servidor.'); return { ok: false, data: {} }; }
  };
  const banner = (cls, icon, title, text) => `<div class="banner ${cls}">${ico(icon)}<div><b>${title}</b>${text}</div></div>`;
  const stockBadge = m => m.stock <= 0 ? '<span class="badge b-crit">Agotado</span>' : m.stock <= m.min ? '<span class="badge b-low">Stock mínimo</span>' : '<span class="badge b-ok">Normal</span>';
  const lotBadge = l => `<span class="badge ${l.st.cls}">${l.st.txt}</span>`;
  const daysTxt = d => d < 0 ? `Venció hace ${-d} d` : d === 0 ? 'Vence hoy' : `${d} día${d === 1 ? '' : 's'}`;
  const role = () => user.role;
  const can = (...r) => r.includes(user.role);

  /* ---- Campana de alertas ---- */
  const renderBell = () => {
    const el = document.getElementById('bell'); if (!el || !S || !can('Administrador', 'Supervisor', 'Farmaceutico')) return;
    const n = S.low.length + S.soon.length + S.expired.length;
    const it = (cls, i, t) => `<div class="it">${ico(i)}<div>${t}</div></div>`;
    const crit = S.low.map(m => it('', 'alert', `<b>${esc(m.n)}</b><br><span class="muted">Stock crítico: ${m.stock} / mín ${m.min}</span>`)).join('');
    const exp = S.expired.map(l => it('', 'x', `<b>${esc(l.m.n)}</b> · ${esc(l.num)}<br><span class="badge b-crit">Vencido</span> ${l.cur} u.`)).join('');
    const soon = S.soon.map(l => it('', 'clock', `<b>${esc(l.m.n)}</b> · ${esc(l.num)}<br><span class="badge b-warn">${daysTxt(l.st.d)}</span> ${l.cur} u.`)).join('');
    el.innerHTML = `<button id="bellBtn" aria-label="Alertas">${ico('bell')}</button>${n ? `<span class="badge-n pulse">${n}</span>` : ''}
      <div class="drop" id="bellDrop"><h4>${ico('bell')} Alertas en tiempo real</h4><div class="list">
      ${S.low.length ? `<div class="sec">Stock crítico (${S.low.length})</div>${crit}` : ''}${S.expired.length ? `<div class="sec">Lotes vencidos (${S.expired.length})</div>${exp}` : ''}
      ${S.soon.length ? `<div class="sec">Vencen en ≤ 30 días (${S.soon.length})</div>${soon}` : ''}${n ? '' : '<div class="empty">Sin alertas pendientes ✔</div>'}</div></div>`;
    document.getElementById('bellBtn').onclick = e => { e.stopPropagation(); document.getElementById('bellDrop').classList.toggle('open'); };
  };

  /* ---- Shell: sprite, menú por rol ---- */
  const buildShell = () => {
    if (!document.getElementById('sprite')) document.body.insertAdjacentHTML('beforeend', `<svg id="sprite" width="0" height="0" style="position:absolute">${Object.entries(ICONS).map(([k, v]) => `<symbol id="i-${k}" viewBox="0 0 24 24">${v}</symbol>`).join('')}</svg>`);
    document.querySelectorAll('[data-i]').forEach(e => e.innerHTML = ico(e.dataset.i) + e.innerHTML);
    const nav = document.getElementById('nav');
    if (nav) nav.innerHTML = NAV.filter(n => n[3].includes(user.role)).map(n => `<a href="${cfg.url(n[0])}" data-page="${n[0]}" class="${n[0] === page ? 'active' : ''}">${ico(n[2])}<span>${n[1]}</span></a>`).join('');
    const ub = document.getElementById('userbox');
    if (ub) ub.innerHTML = `<b>${esc(user.name)}</b><br><span class="role-chip">${ico('shield')} ${user.role}</span>`;
    const bb = document.getElementById('burger'); if (bb) bb.onclick = () => document.getElementById('sidebar').classList.toggle('open');
    document.addEventListener('click', () => document.getElementById('bellDrop')?.classList.remove('open'));
    if (!can('Administrador', 'Supervisor', 'Farmaceutico')) document.getElementById('bell')?.classList.add('hide');
  };
  const setUser = (u, p) => { user = u; page = p; buildShell(); };
  const boot = async fn => { await load(); await fn(S); };
  const reload = async () => { await load(); return S; };

  /* ---- Impresión / PDF (Imprimir > Guardar como PDF) ---- */
  const printHtml = (title, html) => {
    const w = window.open('', '_blank'); if (!w) return alertBox('Ventana bloqueada', 'Permita ventanas emergentes para generar el PDF.', 'warning');
    w.document.write(`<!doctype html><meta charset="utf-8"><title>${esc(title)}</title><style>body{font-family:system-ui,sans-serif;padding:24px;color:#0f172a}h2{color:#1e3a8a;margin:0}table{width:100%;border-collapse:collapse;margin-top:12px}th{background:#1e3a8a;color:#fff;text-align:left;padding:7px}td{padding:6px 7px;border-bottom:1px solid #e2e8f0}.n{text-align:right}.crit td{background:#fef2f2;color:#991b1b}.warn td{background:#fffbeb;color:#92400e}.t{font-size:18px;font-weight:700;text-align:right;margin-top:10px}*{-webkit-print-color-adjust:exact;print-color-adjust:exact}</style>${html}<script>onload=()=>setTimeout(print,300)<\/script>`);
    w.document.close();
  };
  return { NAV, ico, esc, money, iso, midnight, daysTo, fmtDate, lotState, load, reload, boot, setUser, toast, alertBox, api, banner, stockBadge, lotBadge, daysTxt, can, role, cfg, printHtml, renderBell, get S() { return S; }, get user() { return user; } };
})();
