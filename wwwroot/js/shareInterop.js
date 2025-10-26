window.shareInterop = {
  openUrl: function (url) { window.location.href = url; },
  copyText: async function (text) {
    try { await navigator.clipboard.writeText(text); return true; }
    catch { return false; }
  }
};
