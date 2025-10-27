# AdminConfig.razor --- Blazor WASM Drop-in Page

## Overview

This page lets you import, validate, and download JSON configuration
files (`appsettings.json` and `places.json`) without editing source
code.

------------------------------------------------------------------------

## 📄 Code: `Pages/AdminConfig.razor`

``` razor
@page "/admin"
@using System.Text.Json
@using System.Text.Json.Serialization

<h1>Admin — Settings & Nearby Services</h1>
<p class="text-muted">Import, edit, validate, and download JSON files for appsettings and nearby services (hospitals & police).</p>

<div class="container-card p-3 mb-4">
    <h5 class="mb-2">1) appsettings.json (editable subset)</h5>
    <p class="small text-muted mb-2">
        This is a safe subset you can manage here. The downloaded file can replace your current <code>wwwroot/appsettings.json</code>
        (or be reviewed and merged).
    </p>

    <div class="d-flex gap-2 flex-wrap mb-2">
        <input type="file" accept=".json" @onchange="e => OnLoadJsonFile(e, JsonKind.Appsettings)" />
        <button class="btn btn-sm btn-outline-secondary" @onclick="@(()=>SetTemplate(JsonKind.Appsettings))">Load template</button>
        <button class="btn btn-sm btn-outline-primary" @onclick="@(()=>Validate(JsonKind.Appsettings))">Validate</button>
        <button class="btn btn-sm btn-success" @onclick="@(()=>Download(JsonKind.Appsettings, "appsettings.json"))">Download</button>
    </div>

    <textarea class="form-control font-monospace" rows="18" @bind="_appsettingsJson"></textarea>
    @if (!string.IsNullOrWhiteSpace(_appsettingsMsg))
    {
        <div class="mt-2 small">@((MarkupString)_appsettingsMsg)</div>
    }
</div>

<div class="container-card p-3 mb-4">
    <h5 class="mb-2">2) places.json (Hospitals & Police)</h5>
    <p class="small text-muted mb-2">
        The app can use this file to suggest 1–2 **nearest** hospitals and police posts based on the pinned location.
        You can keep this as a static file now and later swap to an API.
    </p>

    <div class="d-flex gap-2 flex-wrap mb-2">
        <input type="file" accept=".json" @onchange="e => OnLoadJsonFile(e, JsonKind.Places)" />
        <button class="btn btn-sm btn-outline-secondary" @onclick="@(()=>SetTemplate(JsonKind.Places))">Load template</button>
        <button class="btn btn-sm btn-outline-primary" @onclick="@(()=>Validate(JsonKind.Places))">Validate</button>
        <button class="btn btn-sm btn-success" @onclick="@(()=>Download(JsonKind.Places, "places.json"))">Download</button>
    </div>

    <textarea class="form-control font-monospace" rows="22" @bind="_placesJson"></textarea>
    @if (!string.IsNullOrWhiteSpace(_placesMsg))
    {
        <div class="mt-2 small">@((MarkupString)_placesMsg)</div>
    }
</div>

<div class="alert alert-info">
    <strong>How to use:</strong>
    <ol class="mb-0">
        <li>Import an existing JSON file or click <em>Load template</em>.</li>
        <li>Edit fields, then click <em>Validate</em>.</li>
        <li>Click <em>Download</em> to save the JSON files.</li>
        <li>Place <code>appsettings.json</code> and <code>places.json</code> under <code>wwwroot/</code> and rebuild/deploy.</li>
    </ol>
</div>

@code {
    private enum JsonKind { Appsettings, Places }

    private string _appsettingsJson = "";
    private string _placesJson = "";
    private string? _appsettingsMsg;
    private string? _placesMsg;

    protected override void OnInitialized()
    {
        // Provide templates on first load
        SetTemplate(JsonKind.Appsettings);
        SetTemplate(JsonKind.Places);
    }

    private void SetTemplate(JsonKind kind)
    {
        // (template JSON truncated for brevity)
    }

    private async Task OnLoadJsonFile(ChangeEventArgs e, JsonKind kind)
    {
        // (file import note and message)
    }

    private void Validate(JsonKind kind)
    {
        // (validation logic)
    }

    private void Require(JsonElement root, string propName)
    {
        if (!root.TryGetProperty(propName, out _))
            throw new InvalidOperationException($"Missing required property: {propName}");
    }

    private void Download(JsonKind kind, string filename)
    {
        // (download logic)
    }
}
```

------------------------------------------------------------------------

## 🗂️ File Structure

-   `wwwroot/appsettings.json` --- Editable configuration subset.
-   `wwwroot/places.json` --- Local dataset for hospitals and police
    posts.

------------------------------------------------------------------------

## 🔒 Security Recommendation

Protect `/admin` using a PIN or basic auth so only trusted operators can
access it.

------------------------------------------------------------------------

## 🔌 Optional Add-ons

-   **PlacesService** --- to load and query nearest entries.
-   **NavLink** to `/admin` --- only visible if
    `FeatureFlags.ShowDebugPanel` is enabled.
