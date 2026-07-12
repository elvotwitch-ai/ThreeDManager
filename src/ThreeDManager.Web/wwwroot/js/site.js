// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

const themeStorageKey = "threedmanager-theme";
const themeToggle = document.getElementById("theme-toggle");

function applyTheme(theme) {
    const normalizedTheme = theme === "light" ? "light" : "dark";
    const isDark = normalizedTheme === "dark";

    document.documentElement.setAttribute("data-bs-theme", normalizedTheme);

    if (themeToggle) {
        themeToggle.setAttribute("aria-pressed", String(isDark));
        themeToggle.querySelector("[data-theme-icon]").textContent = isDark ? "☀️" : "🌙";
        themeToggle.querySelector("[data-theme-label]").textContent = isDark ? "Modo claro" : "Modo escuro";
    }
}

applyTheme(localStorage.getItem(themeStorageKey));

themeToggle?.addEventListener("click", () => {
    const nextTheme = document.documentElement.getAttribute("data-bs-theme") === "dark" ? "light" : "dark";
    localStorage.setItem(themeStorageKey, nextTheme);
    applyTheme(nextTheme);
});
