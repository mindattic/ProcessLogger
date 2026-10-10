// ProcessLogger control surface. Plain vanilla JS talking to the WPF host over
// window.chrome.webview - see MainWindow.xaml.cs for the C# side of this bridge.
(function () {
  'use strict';

  // ---- bridge --------------------------------------------------------------------------------

  function post(action, data) {
    window.chrome.webview.postMessage(Object.assign({ action: action }, data || {}));
  }

  window.addEventListener('error', function (e) {
    post('jsError', { message: e.message, source: e.filename + ':' + e.lineno });
  });
  window.addEventListener('unhandledrejection', function (e) {
    post('jsError', { message: String(e.reason) });
  });

  // ---- column metadata -------------------------------------------------------------------------
  // kind 'value': Excel-style checklist, populated from every distinct value the DB has ever seen.
  // kind 'time': "within the last N minutes" buckets - a checklist of raw timestamps would just be
  // near-unique noise.

  const BLANK = '(blank)';
  const fmtText = function (v) { return (v === null || v === undefined || v === '') ? BLANK : String(v); };
  const fmtNumber = function (v) { return String(v); };
  const fmtDuration = function (v) { return (v === null || v === undefined) ? BLANK : Math.round(v).toLocaleString(); };
  const fmtKb = function (v) { return (v === null || v === undefined) ? '-' : Math.round(v / 1024).toLocaleString(); };

  const ENTITIES = {
    process: {
      idKey: 'processId',
      defaultSort: { col: 'OpenedUtc', desc: true },
      columns: {
        Name: { kind: 'value', fmt: fmtText },
        Title: { kind: 'value', fmt: fmtText },
        ProcessId: { kind: 'value', fmt: fmtNumber },
        ParentProcessName: { kind: 'value', fmt: fmtText },
        Path: { kind: 'value', fmt: fmtText },
        OpenedUtc: { kind: 'time', fmt: fmtTimestamp },
        ClosedUtc: { kind: 'time', fmt: fmtTimestamp },
        DurationMs: { kind: 'value', fmt: fmtDuration },
        PeakWorkingSetBytes: { kind: 'value', fmt: fmtKb },
        FinalWorkingSetBytes: { kind: 'value', fmt: fmtKb },
        SampleCount: { kind: 'value', fmt: fmtNumber },
        Source: { kind: 'value', fmt: fmtText },
      },
    },
    focus: {
      idKey: 'processId',
      defaultSort: { col: 'FocusedAtUtc', desc: true },
      columns: {
        ProcessName: { kind: 'value', fmt: fmtText },
        ProcessId: { kind: 'value', fmt: fmtNumber },
        WindowTitle: { kind: 'value', fmt: fmtText },
        FocusedAtUtc: { kind: 'time', fmt: fmtTimestamp },
        FocusLostAtUtc: { kind: 'time', fmt: fmtTimestamp },
      },
    },
  };

  const TIME_PRESETS_MINUTES = [1, 2, 3, 4, 5, 10, 20, 30, 60];

  // .NET serializes DateTime as e.g. "2026-09-25T18:44:26.9610000". Converting via string slicing
  // (rather than through a Date object) sidesteps any timezone reinterpretation of a value that
  // has no explicit offset.
  function fmtTimestamp(iso) {
    if (!iso) return '';
    var parts = iso.split('T');
    var time = (parts[1] || '').slice(0, 12);
    return parts[0] + ' ' + time;
  }

  // Only place that DOES need real Date arithmetic - explicitly treated as UTC, since these
  // columns are always UTC by convention regardless of what the serializer left off.
  function minutesAgo(iso) {
    if (!iso) return null;
    var z = iso.endsWith('Z') ? iso : iso + 'Z';
    var d = new Date(z);
    if (isNaN(d.getTime())) return null;
    return (Date.now() - d.getTime()) / 60000;
  }

  function rowKey(col) { return col.charAt(0).toLowerCase() + col.slice(1); }

  // ---- state -----------------------------------------------------------------------------------

  function makeTabState(entity) {
    return {
      entity: entity,
      rows: [],
      columnFilters: {}, // col -> {kind, selected: Set<string>|null, knownValues: string[], loaded, withinMinutes}
      sortCol: ENTITIES[entity].defaultSort.col,
      sortDesc: ENTITIES[entity].defaultSort.desc,
    };
  }

  const state = {
    process: makeTabState('process'),
    focus: makeTabState('focus'),
    activeTab: 'process',
  };

  function columnFilter(entity, col) {
    var filters = state[entity].columnFilters;
    if (!filters[col]) {
      filters[col] = {
        kind: ENTITIES[entity].columns[col].kind,
        selected: null, // null = no restriction (everything passes)
        knownValues: [],
        loaded: false,
        withinMinutes: null,
      };
    }
    return filters[col];
  }

  function isColumnFilterActive(cf) {
    return cf.kind === 'time' ? cf.withinMinutes != null : cf.selected != null;
  }

  function rowPasses(entity, row) {
    var filters = state[entity].columnFilters;
    for (var col in filters) {
      var cf = filters[col];
      var raw = row[rowKey(col)];
      if (cf.kind === 'time') {
        if (cf.withinMinutes != null) {
          var age = minutesAgo(raw);
          if (age === null || age > cf.withinMinutes) return false;
        }
      } else if (cf.selected != null) {
        var label = ENTITIES[entity].columns[col].fmt(raw);
        if (!cf.selected.has(label)) return false;
      }
    }
    return true;
  }

  // ---- querying ----------------------------------------------------------------------------------

  function $(id) { return document.getElementById(id); }

  function queryEntity(entity) {
    var prefix = entity;
    post(entity === 'process' ? 'queryProcesses' : 'queryFocus', {
      include: $(prefix + '-include').value,
      exclude: $(prefix + '-exclude').value,
      includeMode: $(prefix + '-match-mode').value,
      excludeMode: $(prefix + '-match-mode').value,
      take: parseInt($(prefix + '-take').value, 10) || 100,
    });
    setError(entity, null);
  }

  function setError(entity, message) {
    var el = $(entity + '-error');
    el.textContent = message || '';
  }

  function setExportStatus(entity, message) {
    var el = $(entity + '-export-status');
    if (el) el.textContent = message || '';
  }

  // ---- CSV export --------------------------------------------------------------------------

  function csvCell(value) {
    var s = String(value);
    return /[",\r\n]/.test(s) ? '"' + s.replace(/"/g, '""') + '"' : s;
  }

  function buildCsv(entity) {
    var columns = Object.keys(ENTITIES[entity].columns);
    var lines = [columns.map(csvCell).join(',')];
    visibleRows(entity).forEach(function (row) {
      lines.push(columns.map(function (col) {
        return csvCell(ENTITIES[entity].columns[col].fmt(row[rowKey(col)]));
      }).join(','));
    });
    return lines.join('\r\n');
  }

  function exportCsv(entity) {
    var stamp = new Date().toISOString().slice(0, 10);
    setExportStatus(entity, '');
    post('exportCsv', {
      entity: entity,
      csv: buildCsv(entity),
      suggestedName: 'processlogger-' + entity + '-' + stamp + '.csv',
    });
  }

  window.processLogger = {
    onProcessRows: function (rows) { state.process.rows = rows; renderTable('process'); },
    onFocusRows: function (rows) { state.focus.rows = rows; renderTable('focus'); },
    onColumnValues: function (payload) {
      var cf = columnFilter(payload.entity, payload.column);
      var fmt = ENTITIES[payload.entity].columns[payload.column].fmt;
      var labels = Array.from(new Set(payload.values.map(fmt)));
      labels.sort(function (a, b) {
        var na = parseFloat(a), nb = parseFloat(b);
        if (!isNaN(na) && !isNaN(nb)) return na - nb;
        return a.localeCompare(b);
      });
      cf.knownValues = labels;
      cf.loaded = true;
      refreshOpenPopover(payload.entity, payload.column);
    },
    onExportSaved: function (payload) {
      setExportStatus(payload.entity, 'Exported to ' + payload.fileName + '.');
    },
    onError: function (payload) {
      var entity = payload.entity || (payload.action === 'queryFocus' ? 'focus' : 'process');
      if (payload.action === 'exportCsv') setExportStatus(entity, 'Export failed: ' + payload.message);
      else setError(entity, 'Invalid filter: ' + payload.message);
    },
  };

  // ---- rendering -----------------------------------------------------------------------------

  // Same filter + sort state renderTable() shows, shared with CSV export so "export" always
  // means "export what you're looking at right now".
  function visibleRows(entity) {
    var rows = state[entity].rows.filter(function (r) { return rowPasses(entity, r); });

    var sortKey = rowKey(state[entity].sortCol);
    var sortDesc = state[entity].sortDesc;
    return rows.slice().sort(function (a, b) {
      var av = a[sortKey], bv = b[sortKey];
      var cmp = av === bv ? 0 : (av === null || av === undefined) ? -1 : (bv === null || bv === undefined) ? 1 : (av < bv ? -1 : 1);
      return sortDesc ? -cmp : cmp;
    });
  }

  function renderTable(entity) {
    var tbody = document.querySelector('#' + entity + '-table tbody');
    var rows = visibleRows(entity);

    var columns = Object.keys(ENTITIES[entity].columns);
    tbody.innerHTML = rows.map(function (row) {
      return '<tr>' + columns.map(function (col) {
        var fmt = ENTITIES[entity].columns[col].fmt;
        return '<td>' + escapeHtml(fmt(row[rowKey(col)])) + '</td>';
      }).join('') + '</tr>';
    }).join('');

    updateSortGlyphs(entity);
    updateFilterButtonStates(entity);
  }

  function escapeHtml(s) {
    return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  }

  function updateSortGlyphs(entity) {
    var table = $(entity + '-table');
    table.querySelectorAll('th').forEach(function (th) {
      var col = th.dataset.col;
      var glyph = th.querySelector('.sort-glyph');
      if (col === state[entity].sortCol) glyph.textContent = state[entity].sortDesc ? '▼' : '▲';
      else glyph.textContent = '';
    });
  }

  function updateFilterButtonStates(entity) {
    var table = $(entity + '-table');
    table.querySelectorAll('th').forEach(function (th) {
      var col = th.dataset.col;
      var btn = th.querySelector('.filter-btn');
      btn.classList.toggle('active', isColumnFilterActive(columnFilter(entity, col)));
    });
  }

  // ---- column filter popover --------------------------------------------------------------------

  var openPopover = null; // {entity, col, el}

  function closePopover() {
    if (!openPopover) return;
    openPopover.el.remove();
    openPopover = null;
  }

  function refreshOpenPopover(entity, col) {
    if (!openPopover || openPopover.entity !== entity || openPopover.col !== col) return;
    var anchor = openPopover.anchor;
    closePopover();
    openColumnPopover(entity, col, anchor);
  }

  function openColumnPopover(entity, col, anchorEl) {
    closePopover();
    var cf = columnFilter(entity, col);
    var pop = document.createElement('div');
    pop.className = 'col-filter-popover';

    if (cf.kind === 'time') {
      pop.appendChild(buildTimeFilterBody(entity, col, cf));
    } else {
      if (!cf.loaded) post('getColumnValues', { entity: entity, column: col });
      pop.appendChild(buildValueFilterBody(entity, col, cf));
    }

    document.body.appendChild(pop);
    var rect = anchorEl.getBoundingClientRect();
    pop.style.top = rect.bottom + 4 + 'px';
    pop.style.left = Math.min(rect.left, window.innerWidth - pop.offsetWidth - 220) + 'px';

    openPopover = { entity: entity, col: col, el: pop, anchor: anchorEl };

    setTimeout(function () {
      document.addEventListener('click', onOutsideClick, { capture: true });
    }, 0);
  }

  function onOutsideClick(e) {
    if (openPopover && !openPopover.el.contains(e.target) && e.target !== openPopover.anchor) {
      document.removeEventListener('click', onOutsideClick, { capture: true });
      closePopover();
      renderTable(state.activeTab); // commit whatever selection changed while it was open
    }
  }

  function buildValueFilterBody(entity, col, cf) {
    var wrap = document.createElement('div');

    var search = document.createElement('input');
    search.type = 'text';
    search.placeholder = 'Search values…';
    wrap.appendChild(search);

    var actions = document.createElement('div');
    actions.className = 'popover-actions';
    var allBtn = document.createElement('button'); allBtn.textContent = 'All';
    var noneBtn = document.createElement('button'); noneBtn.textContent = 'None';
    actions.appendChild(allBtn); actions.appendChild(noneBtn);
    wrap.appendChild(actions);

    var list = document.createElement('div');
    wrap.appendChild(list);

    var checked = cf.selected; // null = "everything checked" (no restriction yet)
    function isChecked(v) { return checked === null || checked.has(v); }

    function renderList(filterText) {
      list.innerHTML = '';
      cf.knownValues
        .filter(function (v) { return !filterText || v.toLowerCase().indexOf(filterText.toLowerCase()) >= 0; })
        .forEach(function (v) {
          var row = document.createElement('label');
          row.className = 'value-row';
          var box = document.createElement('input');
          box.type = 'checkbox';
          box.checked = isChecked(v);
          box.addEventListener('change', function () {
            if (checked === null) checked = new Set(cf.knownValues);
            if (box.checked) checked.add(v); else checked.delete(v);
            cf.selected = checked.size === cf.knownValues.length ? null : new Set(checked);
          });
          var text = document.createElement('span');
          text.textContent = v;
          row.appendChild(box); row.appendChild(text);
          list.appendChild(row);
        });
      if (!cf.knownValues.length) list.textContent = cf.loaded ? '(no values yet)' : 'Loading…';
    }

    search.addEventListener('input', function () { renderList(search.value); });
    allBtn.addEventListener('click', function () { checked = null; cf.selected = null; renderList(search.value); });
    noneBtn.addEventListener('click', function () { checked = new Set(); cf.selected = new Set(); renderList(search.value); });

    renderList('');
    return wrap;
  }

  function buildTimeFilterBody(entity, col, cf) {
    var wrap = document.createElement('div');
    var name = entity + '-' + col + '-time';

    function option(label, minutes) {
      var row = document.createElement('label');
      var radio = document.createElement('input');
      radio.type = 'radio';
      radio.name = name;
      radio.checked = cf.withinMinutes === minutes;
      radio.addEventListener('change', function () {
        cf.withinMinutes = minutes;
        updateFilterButtonStates(entity);
        renderTable(entity);
      });
      var text = document.createElement('span');
      text.textContent = label;
      row.appendChild(radio); row.appendChild(text);
      return row;
    }

    wrap.appendChild(option('Any time', null));
    TIME_PRESETS_MINUTES.forEach(function (m) { wrap.appendChild(option('Within ' + m + ' min', m)); });
    return wrap;
  }

  // ---- wiring --------------------------------------------------------------------------------

  // ---- column widths: resizable + persisted across restarts (localStorage survives in the
  // WebView2 profile's own storage, so no bridge round-trip is needed) ---------------------------

  function columnWidthsKey(entity) { return 'processlogger-columns-' + entity; }

  function applyStoredColumnWidths(entity) {
    var raw;
    try { raw = localStorage.getItem(columnWidthsKey(entity)); } catch (e) { return; }
    if (!raw) return;
    var widths;
    try { widths = JSON.parse(raw); } catch (e) { return; }
    $(entity + '-table').querySelectorAll('colgroup col').forEach(function (col) {
      var w = widths[col.dataset.col];
      if (w) col.style.width = w + 'px';
    });
  }

  function saveColumnWidths(entity) {
    var widths = {};
    $(entity + '-table').querySelectorAll('colgroup col').forEach(function (col) {
      widths[col.dataset.col] = col.offsetWidth;
    });
    try { localStorage.setItem(columnWidthsKey(entity), JSON.stringify(widths)); } catch (e) { /* storage unavailable */ }
  }

  function wireColumnResize(entity, th, colEl) {
    var handle = document.createElement('div');
    handle.className = 'col-resize-handle';
    var startX = 0, startWidth = 0;

    function onMouseMove(e) {
      colEl.style.width = Math.max(40, startWidth + (e.clientX - startX)) + 'px';
    }
    function onMouseUp() {
      handle.classList.remove('resizing');
      document.removeEventListener('mousemove', onMouseMove);
      document.removeEventListener('mouseup', onMouseUp);
      saveColumnWidths(entity);
    }

    handle.addEventListener('mousedown', function (e) {
      e.preventDefault();
      startX = e.clientX;
      startWidth = colEl.offsetWidth;
      handle.classList.add('resizing');
      document.addEventListener('mousemove', onMouseMove);
      document.addEventListener('mouseup', onMouseUp);
    });

    th.appendChild(handle);
  }

  function wireTable(entity) {
    var table = $(entity + '-table');
    applyStoredColumnWidths(entity);
    table.querySelectorAll('th').forEach(function (th) {
      var col = th.dataset.col;
      var inner = document.createElement('div');
      inner.className = 'th-inner';

      var label = document.createElement('span');
      label.className = 'th-label';
      label.textContent = th.textContent;

      var glyph = document.createElement('span');
      glyph.className = 'sort-glyph';

      var filterBtn = document.createElement('button');
      filterBtn.type = 'button';
      filterBtn.className = 'filter-btn';
      filterBtn.textContent = '▾';
      filterBtn.title = 'Filter ' + label.textContent;
      filterBtn.addEventListener('click', function (e) {
        e.stopPropagation();
        if (openPopover && openPopover.col === col && openPopover.entity === entity) { closePopover(); return; }
        openColumnPopover(entity, col, filterBtn);
      });

      inner.appendChild(label);
      inner.appendChild(glyph);
      inner.appendChild(filterBtn);
      inner.addEventListener('click', function (e) {
        if (e.target === filterBtn) return;
        if (state[entity].sortCol === col) state[entity].sortDesc = !state[entity].sortDesc;
        else { state[entity].sortCol = col; state[entity].sortDesc = true; }
        renderTable(entity);
      });

      th.textContent = '';
      th.appendChild(inner);

      var colEl = table.querySelector('colgroup col[data-col="' + col + '"]');
      if (colEl) wireColumnResize(entity, th, colEl);
    });

    $(entity + '-refresh').addEventListener('click', function () { queryEntity(entity); });
    $(entity + '-reset-sort').addEventListener('click', function () {
      state[entity].sortCol = ENTITIES[entity].defaultSort.col;
      state[entity].sortDesc = ENTITIES[entity].defaultSort.desc;
      renderTable(entity);
    });
    $(entity + '-export-csv').addEventListener('click', function () { exportCsv(entity); });
  }

  function wireTabs() {
    document.querySelectorAll('.tab').forEach(function (tab) {
      tab.addEventListener('click', function () {
        document.querySelectorAll('.tab').forEach(function (t) {
          t.classList.remove('active'); t.setAttribute('aria-selected', 'false');
        });
        document.querySelectorAll('.tab-panel').forEach(function (p) { p.classList.remove('active'); });
        tab.classList.add('active');
        tab.setAttribute('aria-selected', 'true');
        $('tab-' + tab.dataset.tab).classList.add('active');
        state.activeTab = tab.dataset.tab;
      });
    });
  }

  var refreshTimer = null;
  function restartRefreshTimer(seconds) {
    if (refreshTimer) clearInterval(refreshTimer);
    refreshTimer = setInterval(function () { queryEntity('process'); queryEntity('focus'); }, seconds * 1000);
  }

  function wireSettings() {
    var slider = $('refresh-interval');
    var label = $('refresh-interval-label');
    slider.addEventListener('input', function () {
      label.textContent = slider.value + 's';
      restartRefreshTimer(parseInt(slider.value, 10));
    });

    $('theme-select').addEventListener('change', function (e) {
      var mode = e.target.value;
      if (mode === 'system') document.documentElement.removeAttribute('data-theme');
      else document.documentElement.dataset.theme = mode;
    });

    var modal = $('settings-modal');
    function openModal() { modal.hidden = false; }
    function closeModal() { modal.hidden = true; }

    $('settings-btn').addEventListener('click', openModal);
    $('settings-close').addEventListener('click', closeModal);
    modal.addEventListener('click', function (e) { if (e.target === modal) closeModal(); });
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && !modal.hidden) closeModal();
    });
  }

  document.addEventListener('DOMContentLoaded', function () {
    wireTabs();
    wireTable('process');
    wireTable('focus');
    wireSettings();
    restartRefreshTimer(parseInt($('refresh-interval').value, 10));
    queryEntity('process');
    queryEntity('focus');
  });
})();
