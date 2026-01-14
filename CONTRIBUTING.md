# 🤝 Guide de Contribution - PaL.Xtreme

Merci de votre intérêt pour contribuer à PaL.Xtreme ! Ce guide vous aidera à démarrer.

## 📋 Table des Matières

- [Code de Conduite](#-code-de-conduite)
- [Prérequis](#-prérequis)
- [Installation](#-installation)
- [Structure du Projet](#-structure-du-projet)
- [Conventions de Code](#-conventions-de-code)
- [Processus de Contribution](#-processus-de-contribution)
- [Tests](#-tests)
- [Documentation](#-documentation)

---

## 📜 Code de Conduite

- Soyez respectueux et bienveillant
- Acceptez les critiques constructives
- Concentrez-vous sur ce qui est le mieux pour la communauté
- Faites preuve d'empathie envers les autres contributeurs

---

## 🔧 Prérequis

| Outil | Version | Téléchargement |
|-------|---------|----------------|
| .NET SDK | 10.0+ | [dotnet.microsoft.com](https://dotnet.microsoft.com/download) |
| PostgreSQL | 15+ | [postgresql.org](https://www.postgresql.org/download/) |
| Visual Studio / VS Code | 2024+ | [visualstudio.com](https://visualstudio.microsoft.com/) |
| Git | 2.40+ | [git-scm.com](https://git-scm.com/) |

---

## 🚀 Installation

### 1. Cloner le dépôt

```bash
git clone https://github.com/DeLTa-X-Tunisia/PaL.Xtreme.git
cd PaL.Xtreme
```

### 2. Configurer la base de données

```bash
# Créer la base de données PostgreSQL
psql -U postgres -c "CREATE DATABASE palxtreme;"

# Mettre à jour la chaîne de connexion dans appsettings.json
```

### 3. Restaurer les dépendances

```bash
dotnet restore
```

### 4. Lancer l'API

```bash
cd PaLX.API
dotnet run
```

### 5. Lancer le Client

```bash
cd PaLX.Client
dotnet run
```

---

## 📁 Structure du Projet

```
PaL.Xtreme/
├── PaLX.API/                 # Backend ASP.NET Core
│   ├── Controllers/          # Points d'entrée API REST
│   ├── Services/             # Logique métier
│   ├── Models/               # Entités de données
│   ├── DTOs/                 # Objets de transfert
│   ├── Hubs/                 # SignalR Hubs (temps réel)
│   └── Scripts/              # Scripts SQL
│
├── PaLX.Client/              # Frontend WPF
│   ├── Services/             # Services client (API, Voice)
│   ├── Views/                # Fenêtres XAML
│   └── Resources/            # Images, sons, styles
│
├── PaLX.Launcher/            # Lanceur avec Health Check
│
├── PaLX.API.Tests/           # Tests unitaires
│
└── docs/                     # Documentation
```

---

## 📝 Conventions de Code

### Nommage

| Type | Convention | Exemple |
|------|------------|---------|
| Classes | PascalCase | `UserService` |
| Méthodes | PascalCase | `GetUserById()` |
| Variables | camelCase | `currentUser` |
| Constantes | UPPER_SNAKE | `MAX_RETRY_COUNT` |
| Champs privés | _camelCase | `_apiService` |

### Style C#

```csharp
// ✅ Bon
public async Task<User?> GetUserByIdAsync(int userId)
{
    if (userId <= 0)
        throw new ArgumentException("Invalid user ID", nameof(userId));
    
    return await _userRepository.FindAsync(userId);
}

// ❌ Mauvais
public async Task<User> getUser(int id) {
    return await _repo.Find(id);
}
```

### XAML

- Utiliser des `StaticResource` pour les styles réutilisables
- Préfixer les noms de contrôles : `btn`, `txt`, `lst`, `grd`
- Commenter les sections complexes

### Commentaires

- Commentaires en **français** pour la documentation
- Utiliser `///` pour les summaries XML
- Expliquer le "pourquoi", pas le "quoi"

```csharp
/// <summary>
/// Vérifie si l'utilisateur a les permissions de modération.
/// Inclut les admins système (niveaux 1-6) et les modérateurs du salon.
/// </summary>
private bool CanUserModerate()
{
    // Les admins système ont toujours accès
    if (_apiService.CurrentUserRoleLevel >= 1 && _apiService.CurrentUserRoleLevel <= 6)
        return true;
    
    // Vérifier le rôle dans le salon
    return _room.UserRole?.ToLower() is "admin" or "moderator" or "superadmin";
}
```

---

## 🔄 Processus de Contribution

### 1. Créer une branche

```bash
# Feature
git checkout -b feature/nom-de-la-fonctionnalite

# Bugfix
git checkout -b fix/description-du-bug

# Documentation
git checkout -b docs/mise-a-jour-readme
```

### 2. Faire vos modifications

- Commits atomiques et descriptifs
- Tester localement avant de pousser

### 3. Format des commits

```
<emoji> <type>: <description>

Exemples :
✨ feat: Ajout système de notifications push
🐛 fix: Correction crash au démarrage
📚 docs: Mise à jour README architecture
🎨 style: Refactoring code RoomWindow
🧪 test: Ajout tests AuthService
⚡ perf: Optimisation requêtes SQL
```

| Emoji | Type | Description |
|-------|------|-------------|
| ✨ | feat | Nouvelle fonctionnalité |
| 🐛 | fix | Correction de bug |
| 📚 | docs | Documentation |
| 🎨 | style | Formatage, refactoring |
| 🧪 | test | Ajout de tests |
| ⚡ | perf | Amélioration performances |
| 🔧 | chore | Maintenance, config |

### 4. Créer une Pull Request

- Titre clair et descriptif
- Description des changements
- Screenshots si UI modifiée
- Référence aux issues liées

---

## 🧪 Tests

### Lancer les tests

```bash
# Tous les tests
dotnet test

# Tests avec couverture
dotnet test --collect:"XPlat Code Coverage"

# Tests spécifiques
dotnet test --filter "FullyQualifiedName~AuthService"
```

### Structure des tests

```csharp
[Fact]
public async Task Login_WithValidCredentials_ReturnsToken()
{
    // Arrange
    var authService = new AuthService(_mockDb.Object);
    var credentials = new LoginDto { Username = "test", Password = "password123" };
    
    // Act
    var result = await authService.LoginAsync(credentials);
    
    // Assert
    Assert.NotNull(result);
    Assert.NotEmpty(result.Token);
}
```

---

## 📖 Documentation

### Mettre à jour la documentation

- **README.md** : Vue d'ensemble et changelog
- **CHANGELOG.md** : Historique détaillé des versions
- **DATABASE.md** : Schéma de base de données
- **CONTRIBUTING.md** : Guide de contribution (ce fichier)

### Générer la documentation API

```bash
# La documentation Swagger est disponible à :
# https://localhost:5001/swagger
```

---

## ❓ Questions ?

- Ouvrez une [Issue](https://github.com/DeLTa-X-Tunisia/PaL.Xtreme/issues)
- Contactez l'équipe sur le serveur de développement

---

**Merci de contribuer à PaL.Xtreme ! 🚀**
