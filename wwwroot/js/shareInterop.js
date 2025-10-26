(function () {
    function safeOpen(url) {
        try {
            const w = window.open(url, "_blank", "noopener,noreferrer");
            if (!w) window.location.href = url;
            return true;
        } catch {
            try { window.location.href = url; return true; }
            catch { return false; }
        }
    }

    function openUrl(url) { return safeOpen(url); }
    function openTel(number) { return safeOpen("tel:" + (number || "")); }

    function openSms(number, body) {
        const isIOS = /iPad|iPhone|iPod/i.test(navigator.userAgent);
        const encoded = body ? encodeURIComponent(body) : "";
        const sep = isIOS ? "&" : "?";
        let url = "sms:" + (number || "");
        if (encoded) url += sep + "body=" + encoded;
        return safeOpen(url);
    }

    function openWhatsApp(number, text) {
        const n = (number || "").replace(/\D+/g, "");
        const base = n ? "https://wa.me/" + n : "https://wa.me/";
        const url = base + (text ? "?text=" + encodeURIComponent(text) : "");
        return safeOpen(url);
    }

    function openMailto(to, subject, body) {
        let url = "mailto:" + (to || "");
        const q = [];
        if (subject) q.push("subject=" + encodeURIComponent(subject));
        if (body) q.push("body=" + encodeURIComponent(body));
        if (q.length) url += "?" + q.join("&");
        return safeOpen(url);
    }

    // Modern clipboard (warning-free)
    async function copyText(text) {
        // 1. Try modern API
        if (navigator.clipboard?.writeText) {
            try {
                await navigator.clipboard.writeText(text);
                return true;
            } catch {
                // 2. Fallback via temporary <input> and selection — no execCommand
                try {
                    const input = document.createElement("input");
                    input.value = text;
                    input.style.position = "fixed";
                    input.style.opacity = "0";
                    document.body.appendChild(input);
                    input.select();
                    // If browser doesn’t allow auto-copy, at least highlight for manual copy
                    document.body.removeChild(input);
                    return false;
                } catch {
                    return false;
                }
            }
        }
        return false;
    }

    function canWebShare() { return !!navigator.share; }
    async function webShare(data) {
        if (!navigator.share) return false;
        try { await navigator.share(data); return true; }
        catch { return false; }
    }

    window.shareInterop = {
        openUrl,
        openTel,
        openSms,
        openWhatsApp,
        openMailto,
        copyText,
        canWebShare,
        webShare
    };
})();
