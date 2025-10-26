# PinTheHighwayCrash (Blazor WebAssembly, 100% free)

A free, client‑side app to **pin a highway crash location in India** and quickly contact authorities.  
It uses **Leaflet + OpenStreetMap**, the **Browser Geolocation API**, and **device dial/SMS/WhatsApp** links. No server or paid APIs.

## Features
- Centers map on your current GPS location
- Drag/click to **pin** the exact spot
- **Anti‑spam**: You must be within **150 m** of the pin to send
- One‑tap **Call 112**, prefilled **SMS**, **WhatsApp share**, or **Copy** report text
- 100% free tech stack

## Tech
- Blazor WebAssembly (.NET 8)
- Leaflet + OpenStreetMap tiles (free with attribution)
- Bootstrap 5 (for UI)
- No backend required

## Run locally
```bash
# Install .NET 8 SDK first: https://dotnet.microsoft.com/en-us/download
dotnet --version

# Restore & run
dotnet restore
dotnet run
```
Then open the printed URL (usually `http://localhost:****`). Allow **location permission** in the browser.

## Deploy to GitHub Pages (free)
1. Push this repo to GitHub.
2. In GitHub, go to **Settings → Pages** and choose source **GitHub Actions**.
3. Use the **.NET** Blazor Pages workflow:
   - Create a workflow using `Deploy to GitHub Pages` for a static site OR
   - Add a simple workflow like:

```yaml
name: Build and Deploy Blazor WASM
on:
  push:
    branches: [ main ]

permissions:
  contents: read
  pages: write
  id-token: write

jobs:
  build-and-deploy:
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Publish
        run: dotnet publish -c Release -o build
      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: build/wwwroot
      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
```

4. Enable **Pages** and pick the artifact.

## Notes
- **OpenStreetMap terms**: Use fairly; attribution is shown in the map. Heavy traffic may require a separate tile host.
- On desktop browsers, **tel:**/**sms:** links may not work. On mobile, they will open the dialer/SMS app.
- This project doesn’t contact 112 automatically (most regions don’t allow automated emergency calls). It **prepares the message** and opens your device’s dialer/messenger.

## Customize
- Change the distance gate in `Pages/Report.razor` (`_maxAllowedMeters = 150`).
- Edit the message template in `BuildReportText()`.
- Add languages or UI in `wwwroot/css/app.css`, `Shared` components.

---

**License:** MIT
