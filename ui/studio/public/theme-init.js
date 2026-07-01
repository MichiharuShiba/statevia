(() => {
  const cookieValue = document.cookie
    .split("; ")
    .find((row) => row.startsWith("ui-theme="))
    ?.split("=")[1];
  const hasCookieTheme = cookieValue === "light" || cookieValue === "dark";
  const prefersDark = !!globalThis.matchMedia?.("(prefers-color-scheme: dark)").matches;
  let theme;
  if (hasCookieTheme) {
    theme = cookieValue;
  } else {
    theme = prefersDark ? "dark" : "light";
  }

  document.documentElement.dataset.theme = theme;
})();
