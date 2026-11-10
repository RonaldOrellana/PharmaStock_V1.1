/* PharmaStock · núcleo de UI: estado, alertas, navegación por rol, utilidades */
const PS = (() => {
    const ALL = ['Administrador', 'Supervisor', 'Farmaceutico', 'Vendedor'];
    const NAV = [ // [pagina, texto, icono, roles permitidos]
        ['Dashboard', 'Panel principal', 'dash', ['Administrador', 'Supervisor', 'Farmaceutico']],
        ['Facturacion', 'Punto de venta', 'cart', ALL],
        ['Catalogo', 'Catálogo / Búsqueda', 'pill', ['Administrador', 'Supervisor']],
        ['Lotes', 'Lotes e ingresos', 'box', ['Administrador', 'Supervisor']],
        ['Movimientos', 'Ajustes y mermas', 'swap', ['Administrador', 'Supervisor']],
        ['Reportes', 'Reportes', 'chart', ['Administrador', 'Supervisor', 'Farmaceutico']],
        ['Usuarios', 'Usuarios', 'shield', ['Administrador']]
    ];

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
        eye: '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>',
        alert: '<path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0zM12 9v4M12 17h.01"/>',
        clock: '<circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/>',
        logout: '<path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9"/>',
        pdf: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6M16 13H8M16 17H8"/>',
        menu: '<path d="M3 12h18M3 6h18M3 18h18"/>', check: '<path d="M20 6L9 17l-5-5"/>', plus: '<path d="M12 5v14M5 12h14"/>',
        search: '<circle cx="11" cy="11" r="8"/><path d="M21 21l-4.35-4.35"/>', x: '<path d="M18 6L6 18M6 6l12 12"/>',
        shield: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>', trend: '<path d="M23 6l-9.5 9.5-5-5L1 18M17 6h6v6"/>'
    };

    const ico = n => `<svg class="ic"><use href="#i-${n}"/></svg>`;
    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const money = n => '$' + Number(n || 0).toFixed(2);
    const midnight = (d = new Date()) => { d = new Date(d); d.setHours(0, 0, 0, 0); return d; };
    const iso = d => { d = new Date(d); return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'); };
    const daysTo = exp => Math.round((midnight(exp + 'T00:00:00') - midnight()) / 864e5);

    const parseMov = s => {
        if (!s) return new Date(0);
        if (s.includes('T') || s.includes('-')) return new Date(s);
        const m = /(\d+)\/(\d+)\/(\d+) (\d+):(\d+)/.exec(s || '');
        return m ? new Date(+m[3], m[2] - 1, +m[1], +m[4], +m[5]) : new Date(s);
    };

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
    const load = async () => {
        const r = await cfg.fetch('/api/state', {
            credentials: 'same-origin'
        });

        if (r.status === 401) {
            location.href = '/Login';
            return;
        }

        const j = await r.json();
        S = S ? Object.assign(S, j) : j;
        derive();
        renderBell();

        return S;
    };
    // Actualizar las alertas automáticamente cada 30 segundos.
    let alertTimer = null;

    const refreshAlerts = async () => {
        if (!S || !user) return;

        if (!can('Administrador', 'Supervisor', 'Farmaceutico','Vendedor')) {
            return;
        }

        try {
            const r = await cfg.fetch('/api/state', {
                method: 'GET',
                credentials: 'same-origin',
                cache: 'no-store'
            });

            if (r.status === 401) {
                location.href = '/Login';
                return;
            }

            if (!r.ok) return;

            const j = await r.json();

            S = Object.assign(S, j);

            derive();

            renderBell();

        } catch (error) {
            console.error('Error actualizando alertas:', error);
        }
    };

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
        const el = document.getElementById('bell'); if (!el || !S || !can('Administrador', 'Supervisor', 'Farmaceutico', 'Vendedor')) return;
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
        if (!nav) return;

        nav.innerHTML = NAV.filter(n => n[3].includes(user.role)).map(n => `<a href="${cfg.url(n[0])}" data-page="${n[0]}" class="${n[0] === page ? 'active' : ''}">${ico(n[2])}<span>${n[1]}</span></a>`).join('');
        const ub = document.getElementById('userbox');
        if (ub) ub.innerHTML = `<b>${esc(user.name)}</b><br><span class="role-chip">${ico('shield')} ${user.role}</span>`;
        const bb = document.getElementById('burger'); if (bb) bb.onclick = () => document.getElementById('sidebar').classList.toggle('open');
        document.addEventListener('click', () => document.getElementById('bellDrop')?.classList.remove('open'));
        if (!can('Administrador', 'Supervisor', 'Farmaceutico','Vendedor')) document.getElementById('bell')?.classList.add('hide');
    };

    const setUser = (u, p) => {
        user = u;
        page = p;

        buildShell();

        // Evitar crear varios temporizadores.
        if (alertTimer) {
            clearInterval(alertTimer);
            alertTimer = null;
        }

        // Actualizar alertas cada 30 segundos.
        if (can('Administrador', 'Supervisor', 'Farmaceutico', 'Vendedor')) {

            alertTimer = setInterval(() => {
                refreshAlerts();
            }, 30000);

        }
    };
    const boot = async fn => { await load(); await fn(S); };
    const reload = async () => { await load(); return S; };

    /* ---- Impresión / PDF ---- */
    const printHtml = (title, html) => {
        const w = window.open('', '_blank'); if (!w) return alertBox('Ventana bloqueada', 'Permita ventanas emergentes para generar el PDF.', 'warning');
        w.document.write(`<!doctype html><meta charset="utf-8"><title>${esc(title)}</title><style>body{font-family:system-ui,sans-serif;padding:24px;color:#0f172a}h2{color:#1e3a8a;margin:0}table{width:100%;border-collapse:collapse;margin-top:12px}th{background:#1e3a8a;color:#fff;text-align:left;padding:7px}td{padding:6px 7px;border-bottom:1px solid #e2e8f0}.n{text-align:right}.crit td{background:#fef2f2;color:#991b1b}.warn td{background:#fffbeb;color:#92400e}.t{font-size:18px;font-weight:700;text-align:right;margin-top:10px}*{-webkit-print-color-adjust:exact;print-color-adjust:exact}</style>${html}<script>onload=()=>setTimeout(print,300)<\/script>`);
        w.document.close();
    };
    /* ---- Códigos de barras: EAN-13 / Code 128 (B y C), sin dependencias ---- */
    const C128 = ['212222', '222122', '222221', '121223', '121322', '131222', '122213', '122312', '132212', '221213', '221312', '231212', '112232', '122132', '122231', '113222', '123122', '123221', '223211', '221132', '221231', '213212', '223112', '312131', '311222', '321122', '321221', '312212', '322112', '322211', '212123', '212321', '232121', '111323', '131123', '131321', '112313', '132113', '132311', '211313', '231113', '231311', '112133', '112331', '132131', '113123', '113321', '133121', '313121', '211331', '231131', '213113', '213311', '213131', '311123', '311321', '331121', '312113', '312311', '332111', '314111', '221411', '431111', '111224', '111422', '121124', '121421', '141122', '141221', '112214', '112412', '122114', '122411', '142112', '142211', '241211', '221114', '413111', '241112', '134111', '111242', '121142', '121241', '114212', '124112', '124211', '411212', '421112', '421211', '212141', '214121', '412121', '111143', '111341', '131141', '114113', '114311', '411113', '411311', '113141', '114131', '311141', '411131', '211412', '211214', '211232', '2331112'];
    const code128 = text => {
        const s = String(text ?? '').replace(/[^\x20-\x7E]/g, '?');

        let vals;

        // Code 128-C: números pares, dos dígitos por símbolo.
        if (/^\d{4,}$/.test(s) && s.length % 2 === 0) {
            vals = [105];

            for (let i = 0; i < s.length; i += 2) {
                vals.push(parseInt(s.slice(i, i + 2), 10));
            }

            // Código antiguo de 13 dígitos:
            // 12 dígitos en Code C y el último en Code B.
            // Esto reduce el ancho sin alterar el número.
        } else if (/^\d{13}$/.test(s)) {
            vals = [105];

            for (let i = 0; i < 12; i += 2) {
                vals.push(parseInt(s.slice(i, i + 2), 10));
            }

            vals.push(100); // Cambio a Code B.
            vals.push(s.charCodeAt(12) - 32);

            // Texto general: Code 128-B.
        } else {
            vals = [104];

            for (const ch of s) {
                vals.push(ch.charCodeAt(0) - 32);
            }
        }

        // Cálculo del dígito verificador Code 128.
        let sum = vals[0];

        for (let i = 1; i < vals.length; i++) {
            sum += vals[i] * i;
        }

        vals.push(sum % 103);

        return vals;
    };
    /* EAN-13 real (95 módulos): lo que la cámara lee mejor. Se usa cuando el código tiene 13 dígitos con verificador válido */
    const EAN_L = ['0001101', '0011001', '0010011', '0111101', '0100011', '0110001', '0101111', '0111011', '0110111', '0001011'];
    const EAN_G = ['0100111', '0110011', '0011011', '0100001', '0011101', '0111001', '0000101', '0010001', '0001001', '0010111'];
    const EAN_R = ['1110010', '1100110', '1101100', '1000010', '1011100', '1001110', '1010000', '1000100', '1001000', '1110100'];
    const EAN_P = ['LLLLLL', 'LLGLGG', 'LLGGLG', 'LLGGGL', 'LGLLGG', 'LGGLLG', 'LGGGLL', 'LGLGLG', 'LGLGGL', 'LGGLGL'];
    const isEan13 = t => /^\d{13}$/.test(t) && ean13Check(t.slice(0, 12)) === +t[12];
    const ean13Bits = t => {
        const par = EAN_P[+t[0]];
        let b = '101';
        for (let i = 0; i < 6; i++) b += (par[i] === 'L' ? EAN_L : EAN_G)[+t[i + 1]];
        b += '01010';
        for (let i = 7; i < 13; i++) b += EAN_R[+t[i]];
        return b + '101';
    };
    const barcodeSvg = (
        text,
        {
            height = 58,
            module = 2.5,
            quiet = 12,
            label = true
        } = {}
    ) => {
        text = String(text ?? '').trim();

        if (!text) {
            return '';
        }

        let bars = '';
        let x = quiet * module;
        let formato = '';

        // EAN-13 solamente cuando el número tiene verificador válido.
        if (isEan13(text)) {
            formato = 'EAN-13';

            const bits = ean13Bits(text);

            for (let i = 0; i < bits.length;) {
                let j = i;

                while (j < bits.length && bits[j] === bits[i]) {
                    j++;
                }

                const w = (j - i) * module;

                if (bits[i] === '1') {
                    bars += `M${x} 0h${w}v${height}h-${w}z`;
                }

                x += w;
                i = j;
            }
        } else {
            // Los códigos antiguos o no válidos como EAN-13
            // se imprimen como Code 128 sin modificar sus números.
            formato = 'Code 128';

            const values = code128(text).concat(106);

            for (const v of values) {
                const pattern = C128[v];

                for (let i = 0; i < pattern.length; i++) {
                    const w = Number(pattern[i]) * module;

                    if (i % 2 === 0) {
                        bars += `M${x} 0h${w}v${height}h-${w}z`;
                    }

                    x += w;
                }
            }
        }

        const W = x + quiet * module;
        const H = height + (label ? 19 : 0);

        return `
        <svg
            xmlns="http://www.w3.org/2000/svg"
            viewBox="0 0 ${W} ${H}"
            width="${W}"
            height="${H}"
            role="img"
            aria-label="${formato}: ${esc(text)}"
            style="
                display:block;
                max-width:100%;
                height:auto;
                margin:0 auto;
                background:#fff;
                shape-rendering:crispEdges;
            "
        >
            <rect
                x="0"
                y="0"
                width="${W}"
                height="${H}"
                fill="#fff"
            />

            <path
                d="${bars}"
                fill="#000"
                shape-rendering="crispEdges"
            />

            ${label
                ? `<text
                        x="${W / 2}"
                        y="${height + 15}"
                        text-anchor="middle"
                        font-family="Arial, sans-serif"
                        font-size="13"
                        font-weight="600"
                        fill="#000"
                    >${esc(text)}</text>`
                : ''
            }
        </svg>
    `;
    };
    /* EAN-13 de uso interno (prefijo 2): 2 + correlativo de 11 dígitos + dígito verificador */
    const ean13Check = d12 => { let s = 0; for (let i = 0; i < 12; i++) s += (+d12[i]) * (i % 2 ? 3 : 1); return (10 - (s % 10)) % 10; };
    const nextBarcode = meds => {
        let max = 0;
        for (const m of meds || []) if (/^2\d{12}$/.test(m.code || '')) max = Math.max(max, parseInt(m.code.slice(1, 12), 10));
        const body = '2' + String(max + 1).padStart(11, '0');
        return body + ean13Check(body);
    };
    /* Etiquetas imprimibles: 'copies' etiquetas de 62 mm con nombre, código de barras y precio */
    const labelsHtml = (m, copies) => {
        const codigo = String(m.code ?? '').trim();

        const one = `
        <div class="lb">
            <div class="ln">${esc(m.n)}</div>

            <div class="barcode-container">
                ${barcodeSvg(codigo, {
            height: 62,
            module: 2.5,
            quiet: 12,
            label: true
        })}
            </div>

            <div class="lp">${money(m.price)}</div>
        </div>
    `;

        return `
        <style>
            @page {
                margin: 8mm;
            }

            * {
                box-sizing: border-box;
                -webkit-print-color-adjust: exact;
                print-color-adjust: exact;
            }

            body {
                background: #fff;
                font-family: Arial, sans-serif;
            }

            .wrap {
                display: flex;
                flex-wrap: wrap;
                gap: 8px;
            }

            .lb {
                width: 62mm;
                min-height: 35mm;
                border: 1px dashed #94a3b8;
                padding: 7px;
                text-align: center;
                background: #fff;
                break-inside: avoid;
                page-break-inside: avoid;
            }

            .ln {
                font-size: 11px;
                font-weight: 700;
                line-height: 1.3;
                margin-bottom: 5px;
                max-height: 2.6em;
                overflow: hidden;
            }

            .barcode-container {
                width: 100%;
                overflow: hidden;
            }

            .barcode-container svg {
                display: block;
                width: 100%;
                max-width: 100%;
                height: auto;
                margin: 0 auto;
            }

            .lp {
                font-size: 14px;
                font-weight: 700;
                margin-top: 3px;
            }
        </style>

        <div class="wrap">
            ${one.repeat(copies)}
        </div>
    `;
    };
    const printLabels = (m, copies = 1) => printHtml('Etiquetas · ' + m.n, labelsHtml(m, Math.max(1, Math.min(200, parseInt(copies, 10) || 1))));
    return { NAV, ico, esc, money, iso, midnight, daysTo, fmtDate, lotState, load, reload, boot, setUser, toast, alertBox, api, banner, stockBadge, lotBadge, daysTxt, can, role, cfg, printHtml, barcodeSvg, code128, nextBarcode, ean13Check, printLabels, renderBell, get S() { return S; }, get user() { return user; } };
})();

// --- GESTIÓN DE USUARIOS (Solo Administrador) ---

async function cargarUsuarios() {
    const tbody = document.getElementById('tbodyUsuarios');

    if (!tbody) {
        console.error('No existe tbodyUsuarios en la vista.');
        return;
    }

    tbody.innerHTML = `
        <tr>
            <td colspan="6" class="text-center p-4">
                Cargando usuarios...
            </td>
        </tr>`;

    try {
        const respuesta = await fetch('/api/users', {
            method: 'GET',
            credentials: 'same-origin',
            headers: {
                'Accept': 'application/json'
            }
        });

        console.log('Estado API usuarios:', respuesta.status);

        if (!respuesta.ok) {
            const error = await respuesta.text();

            console.error('Error API:', error);

            tbody.innerHTML = `
                <tr>
                    <td colspan="6" class="text-center text-danger p-4">
                        Error al consultar usuarios.
                        Código HTTP: ${respuesta.status}
                    </td>
                </tr>`;

            return;
        }

        const users = await respuesta.json();

        console.log('Usuarios recibidos:', users);

        if (!Array.isArray(users) || users.length === 0) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="6" class="text-center p-4">
                        No hay usuarios registrados.
                    </td>
                </tr>`;
            return;
        }


        window.usuariosCache = users;

        tbody.innerHTML = users.map(u => {
            const nombre = u.name ?? u.nombre ?? 'Sin nombre';
            const usuario = u.username ?? u.usuario ?? 'Sin usuario';
            const rol = u.role ?? u.rol ?? 'Sin rol';
            const estado = u.active ?? u.estado ?? false;

            return `
                <tr class="border-bottom border-light">
                    <td class="ps-4 fw-bold text-secondary">
                        #${u.id}
                    </td>

                    <td class="fw-semibold text-dark">
                        ${PS.esc(nombre)}
                    </td>

                    <td>
                        <code>${PS.esc(usuario)}</code>
                    </td>

                    <td>
                        <span class="ps-badge-role">
                            ${PS.esc(rol)}
                        </span>
                    </td>

                    <td>
                        <span class="${estado
                    ? 'ps-badge-active'
                    : 'ps-badge-inactive'}">
                            ${estado ? '● Activo' : '○ Inactivo'}
                        </span>
                    </td>

                    <td class="pe-4 text-end">
                        <button
                            class="btn btn-sm btn-outline-primary"
                            onclick="abrirModalReset(${u.id})">
                            Restablecer clave
                        </button>

                        <button
                            class="btn btn-sm ${estado
                    ? 'btn-outline-danger'
                    : 'btn-outline-success'}"
                            onclick="toggleUsuario(${u.id}, ${estado})">
                            ${estado ? 'Desactivar' : 'Activar'}
                        </button>
                    </td>
                </tr>
            `;
        }).join('');

    } catch (error) {
        console.error('Error cargando usuarios:', error);

        tbody.innerHTML = `
            <tr>
                <td colspan="6" class="text-center text-danger p-4">
                    No se pudo conectar con el servidor.
                </td>
            </tr>`;
    }


}
// =============================================
// MODAL CREAR USUARIO
// =============================================

window.abrirModalUsuario = function () {
    const overlay = document.getElementById('modalUsuarioOverlay');
    if (overlay) {
        overlay.classList.add('active');
        overlay.style.display = 'flex'; // Garantiza visibilidad en pantalla
    } else {
        console.error('No se encontró el elemento #modalUsuarioOverlay');
    }
};

window.cerrarModalUsuario = function () {
    const overlay = document.getElementById('modalUsuarioOverlay');
    if (overlay) {
        overlay.classList.remove('active');
        overlay.style.display = 'none';
    }

    // Limpiar campos del formulario y remover clases de error
    const form = document.getElementById('formUsuario');
    if (form) {
        form.reset();
        form.querySelectorAll('.is-invalid, .bad').forEach(el => el.classList.remove('is-invalid', 'bad'));
    }
};

// Exponer también las funciones de restablecer modal por consistencia
window.cerrarModalReset = cerrarModalReset;
window.guardarPasswordManual = guardarPasswordManual;
window.generarPasswordAutomatica = generarPasswordAutomatica;
window.cargarUsuarios = cargarUsuarios;
window.guardarUsuario = guardarUsuario;
window.toggleUsuario = toggleUsuario;


// Guardar Nuevo Usuario con Validaciones y Alertas
async function guardarUsuario(e) {
    if (e) {
        e.preventDefault();
        e.stopPropagation();
    }

    const nombreEl = document.getElementById('usrNombre');
    const userEl = document.getElementById('usrUsuario');
    const claveEl = document.getElementById('usrClave');
    const rolEl = document.getElementById('usrRol');
    const btnSubmit = e?.target?.querySelector('button[type="submit"]') || document.querySelector('#formUsuario button[type="submit"]');

    if (!nombreEl || !userEl || !claveEl || !rolEl) {
        PS.alertBox('Error del sistema', 'No se encontraron los campos del formulario en la vista.', 'error');
        return;
    }

    [nombreEl, userEl, claveEl, rolEl].forEach(el => el.classList.remove('is-invalid'));

    const nombre = nombreEl.value.trim();
    const usuario = userEl.value.trim();
    const clave = claveEl.value;
    const idRol = parseInt(rolEl.value, 10) || 0;

    const regexNombre = /^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]{3,150}$/;
    const regexUsuario = /^[a-zA-Z0-9_]{6,50}$/;

    if (!nombre) {
        nombreEl.classList.add('is-invalid');
        PS.alertBox('Campo requerido', 'Ingresa el nombre completo del usuario.', 'warning');
        nombreEl.focus();
        return;
    }
    if (!regexNombre.test(nombre)) {
        nombreEl.classList.add('is-invalid');
        PS.alertBox('Nombre inválido', 'El nombre debe contener al menos 3 caracteres y solo letras.', 'warning');
        nombreEl.focus();
        return;
    }

    if (!usuario) {
        userEl.classList.add('is-invalid');
        PS.alertBox('Campo requerido', 'Ingresa un nombre de usuario.', 'warning');
        userEl.focus();
        return;
    }
    if (!regexUsuario.test(usuario)) {
        userEl.classList.add('is-invalid');
        PS.alertBox('Usuario inválido', 'El usuario debe tener al menos 6 caracteres (sin espacios ni caracteres especiales).', 'warning');
        userEl.focus();
        return;
    }

    if (!clave) {
        claveEl.classList.add('is-invalid');
        PS.alertBox('Campo requerido', 'Ingresa la contraseña inicial.', 'warning');
        claveEl.focus();
        return;
    }
    if (clave.length < 6) {
        claveEl.classList.add('is-invalid');
        PS.alertBox('Contraseña débil', 'La contraseña debe tener un mínimo de 6 caracteres.', 'warning');
        claveEl.focus();
        return;
    }

    if (idRol <= 0) {
        rolEl.classList.add('is-invalid');
        PS.alertBox('Rol requerido', 'Selecciona un rol válido para el usuario.', 'warning');
        rolEl.focus();
        return;
    }

    if (btnSubmit) btnSubmit.disabled = true;

    try {
        const data = { nombre, u: usuario, p: clave, idRol };
        const res = await PS.api('/api/users', data);

        if (res.ok && (res.data?.ok === true || res.data?.id > 0)) {
            PS.toast('success', 'Usuario registrado con éxito');

            nombreEl.value = '';
            userEl.value = '';
            claveEl.value = '';
            rolEl.selectedIndex = 0;
            [nombreEl, userEl, claveEl, rolEl].forEach(el => el.classList.remove('is-invalid'));

            if (typeof cerrarModalUsuario === 'function') {
                cerrarModalUsuario();
            } else {
                const modalOverlay = document.getElementById('modalUsuarioOverlay');
                if (modalOverlay) modalOverlay.classList.remove('active');
                const modalEl = document.getElementById('modalUsuario');
                if (modalEl) {
                    modalEl.classList.remove('show');
                    modalEl.style.display = 'none';
                }
            }

            await cargarUsuarios();
        } else {
            PS.alertBox('Atención', res.data?.error || 'No se pudo guardar el usuario.', 'error');
        }
    } catch (err) {
        console.error("Error al guardar usuario:", err);
        PS.alertBox('Error de red', 'Ocurrió un fallo al comunicarse con el servidor.', 'error');
    } finally {
        if (btnSubmit) btnSubmit.disabled = false;
    }
}

// Alternar Estado (Activar / Desactivar)
async function toggleUsuario(id, estadoActual) {
    const accion = estadoActual ? 'desactivar' : 'activar';
    const titulo = estadoActual ? '¿Desactivar usuario?' : '¿Activar usuario?';
    const texto = estadoActual
        ? 'El usuario no podrá acceder al sistema mientras esté inactivo.'
        : 'El usuario recuperará el acceso al sistema.';

    const confirmacion = await Swal.fire({
        title: titulo,
        text: texto,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: estadoActual ? '#dc2626' : '#16a34a',
        cancelButtonColor: '#64748b',
        confirmButtonText: `Sí, ${accion}`,
        cancelButtonText: 'Cancelar',
        reverseButtons: true,
        customClass: {
            popup: 'rounded-4 shadow-lg'
        }
    });

    if (!confirmacion.isConfirmed) return;

    try {
        const res = await fetch(`/api/users/${id}/toggle`, {
            method: 'POST',
            credentials: 'same-origin'
        });

        if (res.ok) {
            PS.toast('success', `Usuario ${estadoActual ? 'desactivado' : 'activado'} correctamente`);
            await cargarUsuarios();
        } else {
            PS.alertBox('Error', 'No se pudo cambiar el estado del usuario.', 'error');
        }
    } catch (err) {
        console.error("Error al cambiar estado:", err);
        PS.alertBox('Error de red', 'No se pudo conectar con el servidor.', 'error');
    }
}

// Registrar Ajuste de Inventario / Mermas
async function aplicarAjuste(e) {
    if (e) e.preventDefault();

    const lot = parseInt(document.getElementById('ajusteLote')?.value, 10);
    const q = parseInt(document.getElementById('ajusteCantidad')?.value, 10);
    const why = document.getElementById('ajusteMotivo')?.value;

    try {
        const res = await fetch('/api/adjust', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify({ lot, q, why })
        });

        const data = await res.json();

        if (res.ok && data.ok) {
            PS.toast('success', 'Ajuste de inventario aplicado correctamente');
            location.reload();
        } else {
            PS.alertBox('Atención', data.error || data.message || 'No se pudo aplicar el ajuste.', 'warning');
        }
    } catch (err) {
        console.error("Error al aplicar ajuste:", err);
        PS.alertBox('Error', 'Fallo al comunicarse con el servidor.', 'error');
    }
}

// -----------------------------------------------------------------------
// 📧 ENVIAR VENTA AL BACKEND Y FACTURAR CON ENVÍO DE CORREO
// -----------------------------------------------------------------------
async function realizarVenta(e) {
    if (e) e.preventDefault();

    // 1. Obtener elementos de la vista
    const emailInput = document.querySelector('input[type="email"]') || document.getElementById('correoCliente');
    const chkEmail = document.querySelector('input[type="checkbox"]') || document.getElementById('chkEnviarCorreo');
    const clienteInput = document.getElementById('nombreCliente');

    const email = emailInput ? emailInput.value.trim() : "";
    const sendEmail = chkEmail ? chkEmail.checked : false;
    const cli = clienteInput ? clienteInput.value.trim() : "Consumidor final";

    // 2. Extraer ítems agregados al carrito
    const items = [];
    const rows = document.querySelectorAll('tr[data-lote], tr[data-lot-id], #tablaCarrito tbody tr');
    rows.forEach(row => {
        const lotId = row.dataset.lote || row.dataset.lotId;
        const cant = row.dataset.cant || row.querySelector('.cant-input, input[type="number"]')?.value || 1;
        if (lotId) {
            items.push({ lot: parseInt(lotId, 10), q: parseInt(cant, 10) });
        }
    });

    if (items.length === 0) {
        PS.alertBox('Carrito vacío', 'Agrega al menos un producto al carrito antes de facturar.', 'warning');
        return;
    }

    if (sendEmail && !email) {
        PS.alertBox('Correo requerido', 'Ingresa una dirección de correo si la casilla de envío está activada.', 'warning');
        return;
    }

    const payload = {
        cli: cli,
        email: email,
        sendEmail: sendEmail,
        items: items
    };

    try {
        const res = await PS.api('/api/sales', payload);

        if (res.ok && res.data?.ok) {
            let infoMsg = `Factura ${res.data.num} emitida correctamente. Total: $${res.data.total.toFixed(2)}`;

            if (res.data.mail) {
                PS.toast('success', '¡Factura enviada al correo correctamente!');
            } else if (res.data.mailError) {
                infoMsg += `\n\n(La venta se realizó, pero no se pudo enviar el correo: ${res.data.mailError})`;
            }

            await Swal.fire({
                icon: 'success',
                title: 'Venta exitosa',
                text: infoMsg,
                confirmButtonColor: '#1e3a8a'
            });

            location.reload();
        } else {
            PS.alertBox('Error al facturar', res.data?.error || 'No se pudo procesar la venta.', 'error');
        }
    } catch (err) {
        console.error("Error al procesar la venta:", err);
        PS.alertBox('Error de conexión', 'Ocurrió un error al contactar al servidor.', 'error');
    }
}

// Escuchar el clic en el botón de facturar
document.addEventListener('click', (e) => {
    const btn = e.target.closest('#btnFacturar, .btn-facturar, button[type="submit"]');
    if (btn && document.querySelector('input[type="email"], #correoCliente')) {
        realizarVenta(e);
    }
});

// =============================================
// RESTABLECIMIENTO DE CONTRASEÑAS
// SOLO ADMINISTRADOR
// =============================================

window.abrirModalReset = function (id) {

    const usuario = (window.usuariosCache || []).find(
        u => Number(u.id) === Number(id)
    );

    if (!usuario) {
        console.error('Usuario no encontrado. ID:', id);
        console.log('Usuarios disponibles:', window.usuariosCache);

        PS.alertBox(
            'Usuario no encontrado',
            'No se pudo localizar el usuario seleccionado.'
        );
        return;
    }

    console.log('Usuario seleccionado:', usuario);

    document.getElementById('resetUserId').value = usuario.id;

    document.getElementById('resetUserName').value =
        `${usuario.name} (${usuario.username})`;

    document.getElementById('resetPassword').value = '';

    document.getElementById('modalResetPassword')
        .classList.add('active');
};

function cerrarModalReset() {
    document.getElementById('modalResetPassword')
        .classList.remove('active');

    document.getElementById('resetPassword').value = '';
}

// CONTRASEÑA MANUAL
async function guardarPasswordManual() {
    const id = document.getElementById('resetUserId').value;
    const password = document.getElementById('resetPassword').value;

    if (!password || password.length < 8) {
        PS.alertBox(
            'Contraseña inválida',
            'La contraseña debe tener al menos 8 caracteres.',
            'warning'
        );
        return;
    }

    const confirmacion = await Swal.fire({
        title: '¿Cambiar contraseña?',
        text: 'La contraseña anterior dejará de funcionar.',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Sí, cambiar',
        cancelButtonText: 'Cancelar',
        confirmButtonColor: '#1e3a8a'
    });

    if (!confirmacion.isConfirmed) return;

    const res = await PS.api(
        `/api/users/${id}/reset-password`,
        {
            p: password,
            generar: false
        }
    );

    if (res.ok && res.data?.ok) {
        cerrarModalReset();

        await Swal.fire({
            icon: 'success',
            title: 'Contraseña actualizada',
            text: 'La nueva contraseña fue guardada correctamente.',
            confirmButtonText: 'Aceptar'
        });
    } else {
        PS.alertBox(
            'Error',
            res.data?.error || 'No se pudo cambiar la contraseña.'
        );
    }
}

// CONTRASEÑA AUTOMÁTICA
async function generarPasswordAutomatica() {
    const id = document.getElementById('resetUserId').value;

    const confirmacion = await Swal.fire({
        title: 'Generar contraseña temporal',
        text: 'Se reemplazará la contraseña actual del usuario.',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Generar contraseña',
        cancelButtonText: 'Cancelar',
        confirmButtonColor: '#16a34a'
    });

    if (!confirmacion.isConfirmed) return;

    const res = await PS.api(
        `/api/users/${id}/reset-password`,
        {
            p: '',
            generar: true
        }
    );

    if (res.ok && res.data?.ok) {
        cerrarModalReset();

        await Swal.fire({
            icon: 'success',
            title: 'Contraseña generada',
            html: `
                <p>Usuario: <b>${PS.esc(res.data.usuario)}</b></p>
                <p>Contraseña temporal:</p>
                <input
                    id="claveTemporalMostrar"
                    class="swal2-input"
                    readonly
                    value="${PS.esc(res.data.password)}"
                    style="font-family:monospace;text-align:center">
                <p class="text-muted">
                    Guarda esta contraseña y entrégasela al usuario.
                </p>
            `,
            showCancelButton: true,
            confirmButtonText: 'Copiar contraseña',
            cancelButtonText: 'Cerrar'
        }).then(async result => {
            if (result.isConfirmed) {
                try {
                    await navigator.clipboard.writeText(
                        res.data.password
                    );
                    PS.toast('success', 'Contraseña copiada');
                } catch {
                    PS.alertBox(
                        'No se pudo copiar',
                        'Puedes copiarla manualmente desde el campo mostrado.',
                        'warning'
                    );
                }
            }
        });

    } else {
        PS.alertBox(
            'Error',
            res.data?.error || 'No se pudo generar la contraseña.'
        );
    }
}