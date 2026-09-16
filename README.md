# R33333AD Optimizer

Optimiseur FPS / système pour Windows (WPF, .NET 8) : tweaks registre journalisés
et réversibles, profils par jeu avec mesure FPS avant/après (PresentMon),
désinstallateur avec chasse aux restes, benchmark, overlay HUD, FR/AR/EN, CLI.

## Contenu
- `src/FPSBooster.App` — application WPF (+ `app.manifest` admin)
- `tests/FPSBooster.Tests` — tests NUnit (56+)
- `Setup/R33333ADOptimizer.iss` — installeur Inno Setup 6
- `.github/workflows/release.yml` — build + tests + installeur + Release GitHub par tag

## Compiler en local
```powershell
dotnet build src/FPSBooster.App/FPSBooster.App.csproj
dotnet test tests/FPSBooster.Tests/FPSBooster.Tests.csproj
dotnet publish src/FPSBooster.App/FPSBooster.App.csproj -c Release -r win-x64 --self-contained true -o publish
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Setup\R33333ADOptimizer.iss
```

## Assets locaux
`src/FPSBooster.App/Assets/bg.mp4` (fond vidéo perso, ~60 Mo) n'est pas
versionné (voir `.gitignore`). L'app démarre sans lui (repli automatique),
pose juste ton fichier à cet endroit pour l'activer en local.

## Sortir une version (déclenche la Release + l'installeur)```powershell
git tag v1.2.1
git push origin v1.2.1
```
La CI compile l'installeur `Setup-R33333ADOptimizer-<version>.exe` et publie
`version.txt` en asset de la Release.

## Mises à jour automatiques (côté utilisateurs)
L'app vérifie un fichier `version.txt` (ligne 1 = version, ligne 2 = URL du setup,
suite = notes). Pointe `update_url` vers :
`https://github.com/<owner>/<repo>/releases/latest/download/version.txt`
et mets `update_auto=1` dans `%LocalAppData%\FPSBooster\settings.txt`.

## Licence
MIT — voir `LICENSE`.
