# 🪜 Step-by-Step: Create a Global `.gitignore`

## 1. Open Git Bash or Terminal
You can do this from anywhere — it applies globally, not per project.

---

## 2. Create a global ignore file
Run this command:
```bash
git config --global core.excludesfile ~/.gitignore_global
```
This tells Git that **every repository** should also use a global ignore file located at:
```
C:\Users\<YourName>\.gitignore_global
```
on Windows (or `~/.gitignore_global` on Linux/macOS).

---

## 3. Create the file
You can create it manually or with one command:
```bash
notepad ~/.gitignore_global
```
If you’re on Linux/macOS, you can use:
```bash
nano ~/.gitignore_global
```

---

## 4. Paste recommended global ignores
Here’s a solid starting template 👇

```
# --- Global ignores (for all repos) ---

# OS files
.DS_Store
Thumbs.db
desktop.ini

# IDE caches
.vs/
*.vsidx
*.user
*.suo
*.userosscache
*.sln.docstates

# Build outputs
bin/
obj/
[Bb]uild/
[Ll]og/
*.cache

# Node / front-end junk
node_modules/
dist/

# Temporary files
*.tmp
*.temp
~$*
*.bak
*.swp

# OneDrive and sync files
*.lnk
*.tmp
*.gid
*.msi
*.msix
*.msixbundle

# Logs
*.log
*.tlog

# JetBrains / Rider
.idea/
*.iml

# VSCode
.vscode/*
!.vscode/settings.json
!.vscode/tasks.json
!.vscode/launch.json
```

Save and close the file.

---

## 5. Confirm Git recognizes it
Run:
```bash
git config --global --get core.excludesfile
```
You should see something like:
```
C:/Users/ajaan/.gitignore_global
```

---

## 6. Test it
Go to one of your repos and try:
```bash
git check-ignore -v .vs/PinTheHighwayCrash/FileContentIndex/whatever.vsidx
```
If configured correctly, Git will show something like:
```
C:/Users/ajaan/.gitignore_global:2:.vs/  .vs/PinTheHighwayCrash/FileContentIndex/whatever.vsidx
```
That means the rule in your **global ignore** file is working ✅
