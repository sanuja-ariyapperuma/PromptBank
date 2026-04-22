/**
 * site.js — Prompt Bank client-side logic
 * Vanilla JS only, no frameworks.
 *
 * Sections:
 *   A) CSRF helper + postJson
 *   B) Copy-to-clipboard
 *   C) Star rating (AJAX)
 *   D) Pin toggle (AJAX)
 *   E) Show-more / Show-less for long prompt content
 */

'use strict';

// ── A) CSRF helper ────────────────────────────────────────────────────────────

/**
 * Reads the anti-forgery token injected by the layout into
 * <meta name="x-csrf-token">.
 * @returns {string}
 */
const csrfToken = () =>
    document.querySelector('meta[name="x-csrf-token"]')?.content ?? '';

/**
 * Sends an authenticated JSON POST request.
 * Attaches the ASP.NET Core anti-forgery token as a request header so
 * [ValidateAntiForgeryToken] / auto-validation on Razor Page handlers passes.
 *
 * @param {string} url   - Relative or absolute URL.
 * @param {object} data  - Plain object that will be JSON-serialised.
 * @returns {Promise<any>} Resolved with the parsed JSON response body.
 * @throws {Error} If the HTTP response status is not OK (2xx).
 */
async function postJson(url, data) {
    const resp = await fetch(url, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': csrfToken()
        },
        body: JSON.stringify(data)
    });
    let responseData = null;
    try { responseData = await resp.json(); } catch { /* non-JSON body */ }
    return { ok: resp.ok, status: resp.status, data: responseData };
}


// ── B) Copy to clipboard ──────────────────────────────────────────────────────

/**
 * Copies a prompt's content to the clipboard when a .btn-copy button is
 * clicked.  The button's data-content attribute holds the text to copy.
 * Gives brief visual feedback ("Copied! ✓") then restores the original label.
 */
function initCopyButtons() {
    document.addEventListener('click', async (e) => {
        const btn = e.target.closest('.btn-copy');
        if (!btn) return;

        const text = btn.dataset.content ?? '';
        if (!text) return;

        try {
            await navigator.clipboard.writeText(text);

            // Visual feedback — preserve the button's original HTML so the
            // icon is restored correctly.
            const original = btn.innerHTML;
            btn.innerHTML = '<i class="bi bi-check-lg me-1"></i>Copied! ✓';
            btn.disabled = true;

            setTimeout(() => {
                btn.innerHTML = original;
                btn.disabled = false;
            }, 2000);
        } catch {
            // Fallback for browsers / contexts that block clipboard API
            btn.textContent = 'Copy failed';
            setTimeout(() => {
                btn.innerHTML = '<i class="bi bi-clipboard me-1"></i>Copy';
            }, 2000);
        }
    });
}


// ── C) Star rating ────────────────────────────────────────────────────────────

/**
 * Handles star-rating button clicks (.star-btn).
 * POSTs to ?handler=Rate on the current page (Index) and then updates:
 *   - The filled / empty state of every star in that card.
 *   - The average score text (.avg-score).
 *   - The vote count text (.vote-count).
 */
function initStarRating() {
    document.addEventListener('click', async (e) => {
        const star = e.target.closest('.star-btn');
        if (!star) return;

        const id    = parseInt(star.dataset.id,   10);
        const stars = parseInt(star.dataset.star, 10);
        if (!id || !stars) return;

        // Optimistic hover feedback — handled by CSS :hover, so we just
        // disable the whole row while the request is in-flight.
        const card    = star.closest('.card');
        const starRow = card?.querySelector('.star-row');
        if (starRow) starRow.style.pointerEvents = 'none';

        try {
            const { ok, status, data } = await postJson('/?handler=Rate', { id, stars });

            if (status === 401) {
                window.location.href = '/Account/Login';
                return;
            }
            if (status === 409) {
                // Already rated — show brief feedback
                if (starRow) {
                    const msg = document.createElement('small');
                    msg.className = 'text-muted ms-1';
                    msg.textContent = 'Already rated';
                    starRow.after(msg);
                    setTimeout(() => msg.remove(), 2000);
                }
                return;
            }
            if (!ok || !data) return;

            const avg   = data.average ?? data.Average ?? 0;
            const count = data.count   ?? data.Count   ?? 0;

            if (starRow) {
                starRow.querySelectorAll('.star-btn').forEach((btn) => {
                    const s = parseInt(btn.dataset.star, 10);
                    const filled = s <= Math.round(avg);
                    btn.classList.toggle('empty', !filled);
                    btn.textContent = filled ? '★' : '☆';
                });
            }

            const avgEl   = card?.querySelector('.avg-score');
            const countEl = card?.querySelector('.vote-count');
            if (avgEl)   avgEl.textContent   = count > 0 ? avg.toFixed(1) : '–';
            if (countEl) countEl.textContent = `(${count} vote${count === 1 ? '' : 's'})`;

        } catch (err) {
            console.error('Rating failed:', err);
        } finally {
            if (starRow) starRow.style.pointerEvents = '';
        }
    });
}


// ── D) Pin toggle ─────────────────────────────────────────────────────────────

/**
 * Handles pin/unpin button clicks (.btn-pin).
 * POSTs to ?handler=TogglePin on the current page (Index) and then toggles:
 *   - Button text ("📌 Unpin" ↔ "📍 Pin") and variant class.
 *   - The "📌 Pinned" badge in the card header (.pin-badge).
 *   - The button's data-pinned attribute so subsequent clicks are correct.
 */
function initPinToggle() {
    document.addEventListener('click', async (e) => {
        const btn = e.target.closest('.btn-pin');
        if (!btn) return;

        const id = parseInt(btn.dataset.id, 10);
        if (!id) return;

        btn.disabled = true;

        try {
            const { ok, status, data } = await postJson('/?handler=TogglePin', { id });

            if (status === 401) {
                window.location.href = '/Account/Login';
                return;
            }
            if (!ok || !data) return;

            const isPinned = data.isPinned ?? data.IsPinned ?? false;

            btn.textContent = isPinned ? '📌 Unpin' : '📍 Pin';
            btn.dataset.pinned = isPinned.toString();
            btn.classList.toggle('btn-warning',          isPinned);
            btn.classList.toggle('btn-outline-secondary', !isPinned);

            const card  = btn.closest('.card');
            const badge = card?.querySelector('.pin-badge');
            if (badge) badge.classList.toggle('d-none', !isPinned);

        } catch (err) {
            console.error('Pin toggle failed:', err);
        } finally {
            btn.disabled = false;
        }
    });
}


// ── E) Show-more / Show-less ──────────────────────────────────────────────────

/**
 * Expands or collapses long prompt content blocks.
 * The button carries data-target="content-{id}" pointing at the <pre>'s id.
 */
function initShowMore() {
    document.addEventListener('click', (e) => {
        const btn = e.target.closest('.show-more-btn');
        if (!btn) return;

        const targetId = btn.dataset.target;
        const pre      = document.getElementById(targetId);
        if (!pre) return;

        const expanded = pre.classList.toggle('expanded');
        btn.setAttribute('aria-expanded', expanded.toString());

        if (expanded) {
            btn.innerHTML = 'Show less <i class="bi bi-chevron-up"></i>';
        } else {
            btn.innerHTML = 'Show more <i class="bi bi-chevron-down"></i>';
        }
    });
}


// ── Bootstrap: wire everything up once the DOM is ready ───────────────────────

document.addEventListener('DOMContentLoaded', () => {
    initCopyButtons();
    initStarRating();
    initPinToggle();
    initShowMore();
    initThemeToggle();
});


// ── F) Theme toggle ───────────────────────────────────────────────────────────

/**
 * Handles the light/dark theme toggle button (#themeToggle).
 * Persists the user's choice in localStorage under 'pb-theme'.
 * Syncs the button icon and aria-label with the active theme.
 */
function initThemeToggle() {
    const btn = document.getElementById('themeToggle');
    if (!btn) return;

    function applyTheme(theme) {
        document.documentElement.setAttribute('data-bs-theme', theme);
        localStorage.setItem('pb-theme', theme);
        const isDark = theme === 'dark';
        btn.innerHTML = isDark
            ? '<i class="bi bi-sun-fill"></i>'
            : '<i class="bi bi-moon-stars-fill"></i>';
        btn.setAttribute('aria-label', isDark ? 'Switch to light mode' : 'Switch to dark mode');
    }

    btn.addEventListener('click', () => {
        const current = document.documentElement.getAttribute('data-bs-theme') || 'dark';
        applyTheme(current === 'dark' ? 'light' : 'dark');
    });

    // Sync icon with whatever theme is currently active (set by anti-flash script or default)
    applyTheme(document.documentElement.getAttribute('data-bs-theme') || 'dark');
}

