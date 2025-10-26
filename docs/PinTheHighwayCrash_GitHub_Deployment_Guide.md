# 🚀 PinTheHighwayCrash — GitHub Deployment Guide

This guide explains how to **deploy your Blazor WebAssembly app** (`PinTheHighwayCrash`) to **GitHub Pages**, so it runs directly from static files — fully offline-capable with your service worker.

---

## 🧰 Prerequisites

1. A **GitHub account** and an existing repository (or create a new one).
2. [.NET 8 SDK](https://dotnet.microsoft.com/download) installed locally.
3. A terminal (PowerShell, CMD, or Bash) with Git access.
4. Your project folder (`PinTheHighwayCrash`) containing:
   - `wwwroot/`
   - `index.html`
   - `service-worker.js`
   - `Program.cs` and supporting files

---

## 🏗️ Step 1 — Publish the App

In your project root, run:

```bash
dotnet publish -c Release -o build
```

This generates a static site build under `/build/wwwroot/`.

---

## 🗂️ Step 2 — Prepare for GitHub Pages

1. Inside the published folder (`build/wwwroot`), create a `.nojekyll` file:

```bash
cd build/wwwroot
echo > .nojekyll
```

This ensures GitHub Pages serves files under `_framework/` correctly.

2. Optional but recommended: Add a `404.html` file (GitHub Pages uses it for fallback routes).

---

## 🌐 Step 3 — Create the `gh-pages` Branch

From your repository root:

```bash
git init
git add .
git commit -m "Initial commit"
git branch -M main
git remote add origin https://github.com/<YourUsername>/<YourRepo>.git
git push -u origin main
```

Then create and push the **deployment branch**:

```bash
git checkout -b gh-pages
git push -u origin gh-pages
```

---

## ⚙️ Step 4 — Deploy the Build Output

Replace your local `gh-pages` content with the published files:

```bash
git checkout gh-pages
rm -rf *  # or del * on Windows
cp -r ../build/wwwroot/* .
git add .
git commit -m "Deploy PinTheHighwayCrash to GitHub Pages"
git push origin gh-pages --force
```

---

## 🧭 Step 5 — Configure GitHub Pages

1. Go to your repository on GitHub.  
2. Click **Settings → Pages**.  
3. Under “Build and Deployment”:
   - **Source:** select **Deploy from branch**
   - **Branch:** choose `gh-pages` / root (`/`)
4. Save.

After a few minutes, your site will be live at:

```
https://<YourUsername>.github.io/<YourRepo>/
```

---

## 🧩 Step 6 — Verify Offline Support

1. Visit your site once **online**.
2. Then switch your browser to **offline mode**.
3. Reload — the app should still load instantly (cached by the service worker).

---

## 🛠️ Troubleshooting

| Problem | Cause | Solution |
|----------|--------|----------|
| Blank page / 404 | Base path mismatch | Edit `<base href="/">` in `index.html` → `<base href="/<YourRepo>/">` |
| Blazor not loading | `_framework` blocked | Ensure `.nojekyll` file exists in root |
| Old version persists | Cache not updated | Increment `SW_VERSION` in `service-worker.js` and redeploy |
| Map tiles missing offline | Never cached | Revisit area once online to pre-cache OSM tiles |

---

## 🌟 Optional: Automate Deployment

You can automate GitHub Pages deployment using GitHub Actions.

Create a workflow file: `.github/workflows/deploy.yml`

```yaml
name: Deploy Blazor WASM to GitHub Pages

on:
  push:
    branches: [ main ]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 8.0.x
      - name: Publish
        run: dotnet publish -c Release -o build
      - name: Deploy to gh-pages
        uses: peaceiris/actions-gh-pages@v3
        with:
          github_token: ${{ secrets.GITHUB_TOKEN }}
          publish_dir: build/wwwroot
```

After pushing this, each commit to `main` will automatically build and update your live site.

---

## ✅ Done!

Your **PinTheHighwayCrash** app is now hosted on **GitHub Pages** — ready to run **fully offline** with cached maps, service worker support, and prefilled emergency contact handling.

