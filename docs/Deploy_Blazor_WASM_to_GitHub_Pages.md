# 🚀 Deploying a Blazor WebAssembly App to GitHub Pages

This guide documents exactly what we did to build and deploy your Blazor WebAssembly app using GitHub Actions and GitHub Pages.

---

## 🧱 Step 1: Prepare the project

1. Ensure your Blazor WebAssembly app builds locally:
   ```bash
   dotnet publish -c Release -p:PublishDir="./build/"
   ```

2. Verify that the published files appear under:
   ```
   /build/wwwroot
   ```

3. Add `/build/` to your `.gitignore` to avoid committing build artifacts.

---

## ⚙️ Step 2: Create GitHub Actions workflow

Create a file at:
```
.github/workflows/deploy.yml
```

Paste this content:

```yaml
name: Deploy Blazor WASM to GitHub Pages

on:
  push:
    branches: [ main ]
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

concurrency:
  group: "pages"
  cancel-in-progress: true

env:
  DOTNET_VERSION: '8.0.x'      # Or 9.0.x if targeting .NET 9
  PUBLISH_DIR: './build/'
  PAGES_DIR: './build/wwwroot'

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
          cache: false

      # Auto-detect project file
      - name: Detect project file
        id: detect
        run: |
          set -e
          csproj=$(git ls-files '*.csproj' | grep -viE 'test|tests' | head -n 1)
          echo "PROJECT_PATH=$csproj" >> $GITHUB_ENV
          echo "Detected project: $csproj"

      - name: Restore
        run: dotnet restore "${{ env.PROJECT_PATH }}"

      - name: Publish (Release)
        run: dotnet publish "${{ env.PROJECT_PATH }}" -c Release -p:PublishDir="${{ env.PUBLISH_DIR }}"

      - name: Adjust base href for GitHub Pages
        run: |
          REPO_NAME="${{ github.event.repository.name }}"
          INDEX_FILE="${{ env.PAGES_DIR }}/index.html"
          sed -i "s#<base href="/" />#<base href="/${REPO_NAME}/" />#g" "$INDEX_FILE"

      - name: Add SPA 404.html and .nojekyll
        run: |
          cp "${{ env.PAGES_DIR }}/index.html" "${{ env.PAGES_DIR }}/404.html"
          touch "${{ env.PAGES_DIR }}/.nojekyll"

      - name: Upload Pages artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: ${{ env.PAGES_DIR }}

  deploy:
    needs: build
    runs-on: ubuntu-latest
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    steps:
      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
```

---

## 🔒 Step 3: Permissions and Pages setup

1. In **Settings → Actions → General**, set:  
   - **Workflow permissions:** `Read and write`

2. In **Settings → Pages**, set:  
   - **Source:** `GitHub Actions`

3. If the repo is private, make it **public** under **Settings → General → Danger Zone → Change visibility**.

---

## ▶️ Step 4: Run and deploy

1. Commit and push to `main`:
   ```bash
   git add .
   git commit -m "Add GitHub Pages deploy workflow"
   git push
   ```

2. Go to the **Actions** tab → wait for the workflow to finish.  
   Once successful, your site will be live at:

   ```
   https://<your-username>.github.io/<your-repo>/
   ```

3. Visit **Settings → Pages** to find the deployed URL.

---

## 💡 Step 5: Verify base href

Ensure your `index.html` in `/wwwroot` has:
```html
<base href="/<repo-name>/">
```

This prevents routing issues when refreshing or navigating inside the app.

---

✅ **Result:**  
Your Blazor WebAssembly app is now automatically built and deployed to GitHub Pages every time you push to `main`.
