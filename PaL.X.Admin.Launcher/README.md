# 🚀 PaL.X.Admin.Launcher

**Launcher moderne et élégant pour le Panel d'Administration PaL.Xtreme React**

![Version](https://img.shields.io/badge/version-1.0.0-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![WPF](https://img.shields.io/badge/WPF-Material%20Design-green)

---

## 📋 Description

Ce launcher fournit une interface graphique moderne permettant aux administrateurs système de :

- 🚀 **Lancer** le Panel d'Administration React en un clic
- 📊 **Surveiller** l'état des services (API et Panel React)
- ⚙️ **Configurer** l'URL de l'API et le port du panel
- 🌐 **Ouvrir** automatiquement le navigateur

---

## ✨ Fonctionnalités

### Interface Moderne
- Design sombre professionnel avec Material Design 3
- Animations fluides et feedback visuel
- Fenêtre sans bordure avec coins arrondis

### Gestion des Services
- Indicateurs de statut en temps réel (API et React)
- Vérification automatique périodique (5 secondes)
- Démarrage/arrêt du serveur de développement Vite

### Configuration
- URL de l'API configurable
- Port du panel React personnalisable
- Option d'ouverture automatique du navigateur
- Sauvegarde automatique des préférences

### Installation Automatique
- Détection des dépendances npm
- Installation automatique si nécessaire

---

## 🛠️ Prérequis

- **Node.js** 18+ et **npm** installés
- **.NET 10.0** Runtime
- **PaL.X.Admin.React** dans le dossier parent

---

## 🚀 Utilisation

### Lancer le Launcher

```bash
# Depuis le dossier du projet
dotnet run --project PaL.X.Admin.Launcher
```

Ou exécutez directement `PaL.X.Admin.Launcher.exe` après compilation.

### Compiler le projet

```bash
dotnet build PaL.X.Admin.Launcher/PaL.X.Admin.Launcher.csproj
```

### Publier une version

```bash
dotnet publish PaL.X.Admin.Launcher -c Release -r win-x64 --self-contained
```

---

## 📁 Structure

```
PaL.X.Admin.Launcher/
├── App.xaml                 # Configuration de l'application
├── App.xaml.cs             # Point d'entrée
├── MainWindow.xaml          # Interface utilisateur
├── MainWindow.xaml.cs       # Logique principale
├── Resources/               # Ressources (icônes, images)
└── PaL.X.Admin.Launcher.csproj
```

---

## ⚙️ Configuration Sauvegardée

Les paramètres sont sauvegardés dans :
```
%APPDATA%/PaL.Xtreme/AdminLauncher/settings.json
```

Contenu :
```json
{
  "ApiUrl": "http://localhost:5001",
  "ReactPort": "5173",
  "AutoOpenBrowser": true
}
```

---

## 🎨 Technologies

| Technologie | Usage |
|-------------|-------|
| **WPF** | Interface utilisateur native Windows |
| **Material Design 3** | Composants UI modernes |
| **HttpClient** | Vérification du statut des services |
| **Process** | Gestion du serveur Node.js |
| **System.Text.Json** | Sauvegarde des paramètres |

---

## 📝 Changelog

### v1.0.0 (Janvier 2026)
- 🎉 Version initiale
- Interface moderne avec Material Design
- Gestion du serveur de développement Vite
- Vérification du statut API/React
- Sauvegarde des préférences

---

## 📄 Licence

© 2026 Azizi Mounir. Tous droits réservés.

---

## 🔗 Voir aussi

- [PaL.X.Admin.React](../PaL.X.Admin.React/) - Panel d'Administration React
- [PaLX.API](../PaLX.API/) - API Backend
- [PaLX.Client](../PaLX.Client/) - Client de messagerie WPF
