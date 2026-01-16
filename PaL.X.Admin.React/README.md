# 🎛️ PaL.Xtreme Admin Panel

Panel d'administration moderne pour PaL.Xtreme, construit avec React, TypeScript et Tailwind CSS.

![Version](https://img.shields.io/badge/version-1.0.0-blue)
![React](https://img.shields.io/badge/React-18.2-61DAFB?logo=react)
![TypeScript](https://img.shields.io/badge/TypeScript-5.3-3178C6?logo=typescript)
![Tailwind CSS](https://img.shields.io/badge/Tailwind_CSS-3.4-06B6D4?logo=tailwindcss)

## ✨ Fonctionnalités

### 📊 Dashboard
- Statistiques en temps réel (utilisateurs, salons, messages)
- Graphiques d'activité
- Aperçu des signalements en attente
- Répartition des abonnements

### 👥 Gestion des Utilisateurs
- Liste paginée avec recherche et filtres
- Détails complets du profil
- Bannissement/Débannissement
- Changement de rôle
- Attribution d'abonnements
- Avertissements

### 🏠 Gestion des Salons
- Vue en grille des salons actifs
- Statistiques par salon
- Fermeture/Suppression
- Filtrage par catégorie

### 🚨 Modération
- File des signalements
- Résolution avec historique
- Actions rapides (bannir, avertir)
- Statuts: En attente, En cours, Résolu, Rejeté

### 🏆 Badges
- Création/Modification de badges
- Système de rareté (Common → Legendary)
- Attribution aux utilisateurs
- Aperçu en temps réel

### 📋 Logs d'Audit
- Historique complet des actions admin
- Recherche dans les logs
- Pagination
- Détails: IP, utilisateur, cible

### ⚙️ Paramètres
- Profil administrateur
- Message broadcast global
- Mode maintenance
- État du système

## 🚀 Installation

```bash
# Naviguer vers le dossier
cd PaL.X.Admin.React

# Installer les dépendances
npm install

# Lancer le serveur de développement
npm run dev
```

L'application sera disponible sur `http://localhost:3000`

## 🔧 Configuration

Créez un fichier `.env` à la racine du projet :

```env
# URL de l'API PaL.Xtreme
VITE_API_URL=http://localhost:5000/api

# URL du Hub SignalR Admin
VITE_SIGNALR_URL=http://localhost:5000/hub/admin
```

## 📦 Structure du Projet

```
PaL.X.Admin.React/
├── public/
│   └── favicon.svg
├── src/
│   ├── contexts/          # React Contexts (Auth, SignalR)
│   │   ├── AuthContext.tsx
│   │   └── SignalRContext.tsx
│   ├── layouts/           # Layouts de page
│   │   └── MainLayout.tsx
│   ├── pages/             # Pages de l'application
│   │   ├── LoginPage.tsx
│   │   ├── DashboardPage.tsx
│   │   ├── UsersPage.tsx
│   │   ├── UserDetailPage.tsx
│   │   ├── RoomsPage.tsx
│   │   ├── ReportsPage.tsx
│   │   ├── BadgesPage.tsx
│   │   ├── LogsPage.tsx
│   │   └── SettingsPage.tsx
│   ├── services/          # Services API
│   │   └── api.ts
│   ├── types/             # Types TypeScript
│   │   └── index.ts
│   ├── App.tsx            # Composant racine + Routing
│   ├── main.tsx           # Point d'entrée
│   └── index.css          # Styles globaux + Tailwind
├── index.html
├── package.json
├── tailwind.config.js
├── tsconfig.json
└── vite.config.ts
```

## 🔗 Communication avec l'API

Le panel utilise :

### REST API
Pour toutes les opérations CRUD (users, rooms, reports, badges, logs)

### SignalR
Pour les événements temps réel :
- `UserConnected` / `UserDisconnected`
- `NewReport`
- `UserBanned`
- `StatsUpdated`
- `BroadcastMessage`

## 🎨 Design System

### Couleurs
- **Primary**: `#6366f1` (Indigo/Violet)
- **Success**: `#10b981` (Vert)
- **Warning**: `#f59e0b` (Orange)
- **Danger**: `#ef4444` (Rouge)
- **Dark**: `#0f172a` → `#f8fafc` (Échelle de gris)

### Composants CSS
- `.card` / `.card-hover` - Cartes avec glassmorphism
- `.btn-*` - Boutons (primary, secondary, danger, ghost)
- `.badge-*` - Badges de statut
- `.input` - Champs de formulaire
- `.table` - Tableaux stylisés
- `.modal` - Modals avec overlay

## 🛡️ Sécurité

- Authentification JWT requise
- Vérification du rôle (Admin, SuperAdmin, Moderator)
- Token refresh automatique
- Redirection vers login si non autorisé

## 📝 Scripts Disponibles

```bash
npm run dev      # Développement avec hot reload
npm run build    # Build de production
npm run preview  # Prévisualisation du build
npm run lint     # Vérification ESLint
```

## 🔄 Prochaines Améliorations

- [ ] Mode sombre / clair toggle
- [ ] Notifications push
- [ ] Export des données (CSV, Excel)
- [ ] Graphiques avancés
- [ ] Gestion des abonnements Stripe
- [ ] 2FA pour administrateurs

## 📄 License

Ce projet fait partie de PaL.Xtreme et est sous licence MIT.

---

**Développé avec ❤️ pour PaL.Xtreme**
