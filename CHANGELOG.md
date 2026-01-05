# Changelog - PaL.Xtreme

Toutes les modifications importantes de ce projet seront documentées dans ce fichier.

Le format est basé sur [Keep a Changelog](https://keepachangelog.com/fr/1.0.0/),
et ce projet adhère au [Semantic Versioning](https://semver.org/lang/fr/).

---

## [1.3.0] - 2026-01-05

### ✨ Nouvelles fonctionnalités
- **Mode Sombre** : Système de thème complet Light/Dark
  - Toggle dans les paramètres pour basculer entre les thèmes
  - Sauvegarde automatique des préférences utilisateur
  - Palette dark moderne (#1A1A2E, #25253D, #EAEAEA)
  - Utilisation de DynamicResource pour changement instantané

- **Fenêtre Paramètres** : Nouvelle interface de configuration
  - Design épuré et compact sans scroll
  - Icônes colorées pour chaque option (🌙🔔🎵🔥)
  - Options : Mode Sombre, Sons notification, Son démarrage
  - Section À propos avec version et copyright
  - Fenêtre non-modale (ne bloque plus l'application)

### 🎨 Améliorations UI
- **Barre de Navigation Moderne** :
  - Design "floating" avec effet de profondeur
  - Boutons avec fond arrondi stylisé
  - Bouton central "+" mis en valeur avec ombre rouge
  - Hover effect moderne avec SurfaceBrush

- **Menu Contextuel Amélioré** (⚙️) :
  - Icônes colorées avec fond (bleu/violet/rouge)
  - Titres avec descriptions explicites
  - Padding et espacement optimisés

---

## [1.2.0] - 2026-01-05

### ✨ Nouvelles fonctionnalités
- **UserProfiles** : Nouveau design moderne avec layout 2 colonnes
  - Panneau gauche avec avatar (120x120) et gradient rouge
  - Formulaire sans scroll, contrôles plus grands
  - Interface harmonieuse et moderne

### 🐛 Corrections
- **Appel vocal** : Correction du bug où le destinataire continuait à sonner quand l'appelant raccrochait avant réponse
  - Ajout du suivi des appels sortants en attente (`_pendingOutgoingCalls`)
  - Le destinataire voit maintenant "*{Nom} a annulé l'appel*" et sa fenêtre se ferme

### 🎨 Améliorations UI
- **Transfert de fichiers** : Icônes personnalisées par type de fichier (Excel, Word, PDF, ZIP/RAR)
- **Chat** : Couleur bleue (#3498DB) pour les messages de déblocage
- **Modal Bloquer** : Affichage du DisplayName au lieu du username

---

## [1.1.0] - 2026-01-04

### 🐛 Corrections
- **Lecteur vidéo** : Correction du bug de fenêtre fantôme
- **Playback vidéo** : Améliorations UX

---

## [1.0.0] - 2026-01-03

### ✨ Nouvelles fonctionnalités
- **Chat modernisé** : Templates natifs WPF, lecteurs audio/vidéo intégrés
- **Gestion des salons** : Implémentation complète (API, Client, Admin)
- **Appels vocaux** : Système d'appel P2P avec WebRTC

---

## Versioning

- **MAJOR** (1.x.x) : Changements incompatibles avec les versions précédentes
- **MINOR** (x.1.x) : Nouvelles fonctionnalités rétro-compatibles  
- **PATCH** (x.x.1) : Corrections de bugs rétro-compatibles
