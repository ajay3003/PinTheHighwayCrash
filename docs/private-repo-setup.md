# Push PinTheHighwayCrash to a Private GitHub Repository

This guide explains how to push the **PinTheHighwayCrash** project to a **private GitHub repo** using either HTTPS with a Personal Access Token (PAT) or SSH keys.  
All commands work on Windows with **Git Bash** or **PowerShell**.

---

## Option A — HTTPS with Personal Access Token (simple)

### 1. Create a new private repository
- Go to [https://github.com/new](https://github.com/new)
- Name it `PinTheHighwayCrash`
- Set visibility to **Private**
- **Do not** initialize with a README (keep it empty)

### 2. Initialize and connect locally
```bash
cd /c/path/to/PinTheHighwayCrash
git init
git add .
git commit -m "Initial commit"
git branch -M main
git remote add origin https://github.com/<YOUR-USERNAME>/<YOUR-PRIVATE-REPO>.git
```

### 3. Clear any stale credentials
#### GUI:
1. Open **Control Panel → User Accounts → Credential Manager**
2. Choose **Windows Credentials**
3. Remove all entries containing **github.com**

#### Command-line (newer Git versions):
```bash
git config --global credential.helper manager
git credential-manager erase
protocol=https
host=github.com
# press Enter twice after 'host='
```

### 4. Push and authenticate
```bash
git push -u origin main
```

When prompted:
- **Username:** your GitHub username (e.g., `ajay3003`)
- **Password:** your **Personal Access Token (classic)**

Create PAT:
1. GitHub → **Settings → Developer settings → Personal access tokens → Tokens (classic)**
2. **Generate new token**
3. Scopes: ✅ `repo` and (optional) `workflow`
4. If under an organization with SSO, click **Authorize SSO**.

---

## Option B — SSH (recommended long-term)

### 1. Generate a key pair
```bash
ssh-keygen -t ed25519 -C "your_email@example.com"
# Press Enter for defaults (~/.ssh/id_ed25519)
```

### 2. Add the public key to GitHub
```bash
cat ~/.ssh/id_ed25519.pub
```
Copy the output → GitHub → **Settings → SSH and GPG keys → New SSH key** → Paste → Save.

### 3. Test and set SSH as remote
```bash
ssh -T git@github.com   # should say “Hi <username>!”
git remote remove origin  # optional if HTTPS was added
git remote add origin git@github.com:<YOUR-USERNAME>/<YOUR-PRIVATE-REPO>.git
git push -u origin main
```

---

## Verify configuration
```bash
git remote -v
git config user.name
git config user.email
```

---

## Common issues

| Symptom | Likely Cause | Fix |
|----------|--------------|-----|
| 403 on push | Cached wrong credentials | Clear via Credential Manager |
| PAT rejected | Missing `repo` scope or not SSO authorized | Regenerate PAT |
| Push fails to private repo | No write access | Verify repo ownership |
| SSH key ignored | Key not added in GitHub | Add via Settings → SSH keys |

---

## Optional: Add a GitHub Actions build workflow
To automatically build your Blazor app when you push:
```yaml
# .github/workflows/build.yml
name: Build and Verify
on: [push]
permissions:
  contents: read
  actions: read
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet build -c Release
```

---

**Done! 🎉**  
You now have a private GitHub repository with secure authentication and CI build setup.
