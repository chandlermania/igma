// Navigation progress bar — visible while the browser waits for the next page (incl. Azure fetches)
(function () {
    const track = document.createElement('div');
    track.className = 'progress';
    track.style.cssText = '--cui-progress-height:3px;position:fixed;top:0;left:0;right:0;z-index:9999;border-radius:0;background:transparent;opacity:0;transition:opacity 0.2s;pointer-events:none';

    const bar = document.createElement('div');
    bar.className = 'progress-bar';
    bar.setAttribute('role', 'progressbar');
    bar.style.setProperty('--cui-progress-bar-transition', 'none');
    bar.style.width = '0%';

    track.appendChild(bar);
    document.body.appendChild(track);

    function start() {
        bar.style.setProperty('--cui-progress-bar-transition', 'none');
        bar.style.width = '0%';
        track.style.opacity = '1';
        requestAnimationFrame(() => requestAnimationFrame(() => {
            bar.style.setProperty('--cui-progress-bar-transition', 'width 12s cubic-bezier(0.1, 0.05, 0, 1)');
            bar.style.width = '90%';
        }));
    }

    // Reset if page is restored from bfcache (back/forward)
    window.addEventListener('pageshow', e => {
        if (e.persisted) {
            track.style.opacity = '0';
            bar.style.setProperty('--cui-progress-bar-transition', 'none');
            bar.style.width = '0%';
        }
    });

    document.addEventListener('click', e => {
        const a = e.target.closest('a[href]');
        if (!a || a.target || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
        try {
            if (new URL(a.href, location.href).origin === location.origin) start();
        } catch {}
    });

    // Show for GET form submissions (search); POST forms use fetch and don't navigate
    document.addEventListener('submit', e => {
        if (e.target.method?.toLowerCase() !== 'post') start();
    });
})();

document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('table[data-sortable]').forEach(table => {
        table.querySelectorAll('thead th').forEach((th, colIndex) => {
            if (th.textContent.trim() === '') return;
            th.classList.add('sortable-col');
            th.addEventListener('click', () => sortTable(table, th, colIndex));
        });
    });
});

function sortTable(table, clickedTh, colIndex) {
    const tbody = table.tBodies[0];
    const rows = Array.from(tbody.rows);
    const asc = clickedTh.dataset.sortDir !== 'asc';

    rows.sort((a, b) => {
        const aText = a.cells[colIndex]?.textContent.trim() ?? '';
        const bText = b.cells[colIndex]?.textContent.trim() ?? '';
        const aNum = parseFloat(aText);
        const bNum = parseFloat(bText);
        const cmp = (!isNaN(aNum) && !isNaN(bNum))
            ? aNum - bNum
            : aText.localeCompare(bText, undefined, { sensitivity: 'base' });
        return asc ? cmp : -cmp;
    });

    rows.forEach(row => tbody.appendChild(row));

    table.querySelectorAll('thead th').forEach(th => {
        delete th.dataset.sortDir;
        th.classList.remove('sort-asc', 'sort-desc');
    });
    clickedTh.dataset.sortDir = asc ? 'asc' : 'desc';
    clickedTh.classList.add(asc ? 'sort-asc' : 'sort-desc');
}

// Submit navbar search when the browser's native clear (×) button is clicked
document.getElementById('navSearchInput')?.addEventListener('search', function () {
    document.getElementById('navSearchForm').submit();
});
