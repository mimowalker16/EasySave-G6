# EasySave v1.0 — Logiciel de sauvegarde

> Projet réalisé dans le cadre d'une mission pour **ProSoft** par l'équipe de développement CESI.  
> Application console .NET 8.0 implémentant l'architecture **MVVM** avec une bibliothèque de logs dédiée (**EasyLog**).

---

## 📋 Contexte du projet

ProSoft est un éditeur de logiciels spécialisé dans les solutions de gestion pour les PME.  
Dans le cadre de l'évolution de son catalogue, ProSoft a commandé le développement d'un logiciel de sauvegarde professionnel nommé **EasySave**.

### Exigences principales

- Application **.NET 8.0** en mode console (évolutive vers une interface graphique)
- Architecture **MVVM** (Model – View – ViewModel) obligatoire
- Module de logs **séparé** sous forme de DLL (`EasyLog`)
- Logs journaliers au format **JSON**
- Suivi d'état en **temps réel** (fichier `state.json`)
- Support **multi-langue** : Français 🇫🇷 et Anglais 🇬🇧
- Maximum **5 travaux de sauvegarde** configurables
- Types de sauvegarde : **Complète** et **Différentielle**

---

## 🗂️ Structure du projet

```
logiciel/
├── EasySave/                    ← Application console principale
│   ├── Program.cs               ← Point d'entrée (Main)
│   ├── Models/
│   │   ├── BackupJob.cs         ← Configuration d'un travail de sauvegarde
│   │   ├── BackupState.cs       ← État en temps réel d'un job
│   │   ├── BackupType.cs        ← Enum : Full / Differential
│   │   └── BackupStateType.cs   ← Enum : Inactive / Active / End
│   ├── Services/
│   │   ├── BackupService.cs     ← Moteur de copie de fichiers
│   │   ├── ConfigService.cs     ← Persistance des jobs (jobs.json)
│   │   └── StateService.cs      ← Suivi d'état en temps réel (state.json)
│   ├── ViewModels/
│   │   └── BackupViewModel.cs   ← Chef d'orchestre MVVM (logique métier)
│   ├── Views/
│   │   └── ConsoleView.cs       ← Interface utilisateur console
│   └── Localization/
│       ├── LanguageManager.cs   ← Gestionnaire de traduction
│       ├── en.json              ← Textes en anglais
│       └── fr.json              ← Textes en français
│
├── EasyLog/                     ← Bibliothèque DLL de logs (projet séparé)
│   └── Logger.cs                ← Logger JSON thread-safe
│
├── TestData/                    ← Dossiers de test fournis
│   ├── Source1/                 ← Dossier source pour les tests
│   ├── Source2/
│   ├── Target1/                 ← Dossier cible pour les tests
│   └── Target2/
│
├── docs/                        ← Documentation
│   ├── user_manual.md           ← Manuel utilisateur
│   └── uml/                     ← Diagrammes UML (PlantUML)
│
├── EasySave.slnx                ← Fichier solution Visual Studio
└── README.md                    ← Ce fichier
```

---

## 🚀 Lancer le projet depuis Visual Studio

### Prérequis

- **Visual Studio 2022** (version 17.10 ou supérieure)
- **.NET 8.0 SDK** installé ([télécharger ici](https://dotnet.microsoft.com/download/dotnet/8.0))

### Étape 1 — Ouvrir la solution

1. Lance **Visual Studio 2022**
2. Sur l'écran d'accueil → clique sur **"Ouvrir un projet ou une solution"**
3. Navigue jusqu'à : `C:\Users\DELL\Desktop\logiciel`
4. Sélectionne le fichier **`EasySave.slnx`** → clique **Ouvrir**

### Étape 2 — Vérifier l'Explorateur de solutions

Dans le panneau **Explorateur de solutions** (à droite), tu dois voir :

```
📁 Solution 'EasySave'
   ├── 📁 EasySave        ← application principale
   └── 📁 EasyLog         ← bibliothèque de logs (DLL)
```

### Étape 3 — Définir le projet de démarrage

- Fais un **clic droit** sur le projet **`EasySave`**
- Clique sur **"Définir comme projet de démarrage"**

### Étape 4 — Lancer l'application

| Action | Raccourci |
|--------|-----------|
| Lancer **avec** débogage | `F5` |
| Lancer **sans** débogage | `Ctrl + F5` |

> ✅ La fenêtre console s'ouvre avec le menu ASCII d'EasySave.

### Étape 5 — (Si nécessaire) Restaurer les packages NuGet

Si Visual Studio signale des erreurs de packages au premier lancement :  
**Menu** → `Outils` → `Gestionnaire de packages NuGet` → `Restaurer les packages NuGet`  
Ou : **clic droit sur la Solution** → `Restaurer les packages NuGet`

---

## 🖥️ Utilisation — Les 8 options du menu

```
╔══════════════════════════════════════════════════════════╗
║         ███████╗ █████╗ ███████╗██╗   ██╗               ║
║                    EasySave  v1.0                        ║
╠══════════════════════════════════════════════════════════╣
║  1. List backup jobs                                     ║
║  2. Create a backup job                                  ║
║  3. Edit a backup job                                    ║
║  4. Delete a backup job                                  ║
║  5. Execute a backup job                                 ║
║  6. Execute ALL backup jobs                              ║
║  7. Change language                                      ║
║  8. Quit                                                 ║
╚══════════════════════════════════════════════════════════╝
```

---

### Option 1 — Lister les travaux de sauvegarde

Affiche tous les jobs configurés avec leur numéro, nom, type et dossier source.

```
  1 | Sauvegarde Docs      | Full          | C:\TestData\Source1
  2 | Sauvegarde Perso     | Differential  | C:\TestData\Source2
```

Si aucun job n'est configuré, le message `No backup jobs configured` s'affiche.

---

### Option 2 — Créer un travail de sauvegarde

Crée un nouveau job de sauvegarde. L'application pose **4 questions** :

| Champ | Description | Exemple |
|-------|-------------|---------|
| **Job name** | Nom unique du job (max 50 caractères) | `Sauvegarde Documents` |
| **Source directory** | Chemin complet du dossier **à sauvegarder** | `C:\Users\DELL\Desktop\logiciel\TestData\Source1` |
| **Target directory** | Chemin complet du dossier **de destination** | `C:\Users\DELL\Desktop\logiciel\TestData\Target1` |
| **Type (1=Full / 2=Differential)** | Type de sauvegarde | `1` ou `2` |

#### 🔵 Source Directory — Qu'est-ce que c'est ?

C'est le **dossier que tu veux sauvegarder** (la source). Il doit obligatoirement **exister** sur ton PC ou réseau. Tous les fichiers qu'il contient seront copiés vers la destination.

Exemples réels :
- `C:\Users\DELL\Documents` → sauvegarde tous tes documents
- `C:\Projet\MonCode` → sauvegarde un projet
- `C:\Users\DELL\Desktop\logiciel\TestData\Source1` → pour les tests

#### 🟢 Target Directory — Qu'est-ce que c'est ?

C'est **l'endroit où les fichiers seront copiés** (la destination). Ce dossier sera créé automatiquement s'il n'existe pas.

Exemples réels :
- `D:\Backup\Documents` → sauvegarde sur un deuxième disque
- `\\NAS\Backup` → sauvegarde sur un serveur réseau
- `C:\Users\DELL\Desktop\logiciel\TestData\Target1` → pour les tests

#### Types de sauvegarde

| Type | Comportement |
|------|-------------|
| **Full (Complète)** | Copie **tous** les fichiers, à chaque exécution |
| **Differential (Différentielle)** | Copie uniquement les fichiers **nouveaux ou modifiés** depuis la dernière sauvegarde |

> ⚠️ Maximum **5 jobs** autorisés. Les noms doivent être uniques.

---

### Option 3 — Modifier un travail de sauvegarde

Modifie un job existant par son numéro.  
Pour chaque champ, si tu laisses la saisie **vide** → la valeur actuelle est conservée.

---

### Option 4 — Supprimer un travail de sauvegarde

Supprime définitivement un job par son numéro.  
La configuration est immédiatement mise à jour dans `jobs.json`.

---

### Option 5 — Exécuter un travail de sauvegarde

Lance la copie de fichiers pour **un seul job** sélectionné par son numéro.

**Déroulement :**
1. La liste des jobs disponibles s'affiche
2. Tu entres le numéro du job
3. La copie démarre (Source → Target)
4. L'état est mis à jour en temps réel dans `state.json`
5. Chaque fichier copié est enregistré dans le log journalier

---

### Option 6 — Exécuter tous les travaux

Lance **tous les jobs configurés** séquentiellement, dans l'ordre.  
À la fin : message de succès global ou liste des erreurs rencontrées.

---

### Option 7 — Changer la langue

Bascule l'interface entre **Anglais** et **Français**.

```
  1. English
  2. Français
```

Tape `1` pour l'anglais, `2` pour le français.

---

### Option 8 — Quitter

Ferme l'application proprement.

---

## 🧪 Guide de test rapide

### Prérequis : créer des fichiers de test

Avant de tester, crée des fichiers dans `TestData\Source1` :

```powershell
# Dans PowerShell, à la racine du projet
echo "Fichier test 1" > "TestData\Source1\fichier1.txt"
echo "Fichier test 2" > "TestData\Source1\fichier2.txt"
echo "Document important" > "TestData\Source1\doc.txt"
```

### Scénario de test complet

| # | Option | Saisies | Résultat attendu |
|---|--------|---------|-----------------|
| 1 | `2` Créer | Nom: `TestFull`, Source: `...\TestData\Source1`, Target: `...\TestData\Target1`, Type: `1` | ✔ Job créé |
| 2 | `1` Lister | — | Job `TestFull` visible |
| 3 | `5` Exécuter | Choix: `1` | ✔ Fichiers copiés dans Target1 |
| 4 | `2` Créer | Nom: `TestDiff`, Source: `...\TestData\Source1`, Target: `...\TestData\Target2`, Type: `2` | ✔ Job créé |
| 5 | `5` Exécuter | Choix: `2` | ✔ Seuls les fichiers différents copiés |
| 6 | `6` Tout exécuter | — | ✔ Les 2 jobs s'exécutent |
| 7 | `7` Langue | `2` | Interface passe en français |
| 8 | `3` Modifier | Choix: `1`, nouveau nom: `TestFull_V2` | ✔ Job renommé |
| 9 | `4` Supprimer | Choix: `2` | ✔ TestDiff supprimé |
| 10 | `8` Quitter | — | `Goodbye / Au revoir!` |

### Vérifier les fichiers générés

Après les tests, contrôle les fichiers suivants :

| Fichier | Emplacement | Contenu |
|---------|-------------|---------|
| **Fichiers copiés** | `TestData\Target1\` et `Target2\` | Copies de Source1 |
| **Logs journaliers** | `%APPDATA%\EasySave\Logs\YYYY-MM-DD.json` | Historique des transferts |
| **État temps réel** | `%APPDATA%\EasySave\state.json` | Dernier état des jobs |
| **Configuration** | `%APPDATA%\EasySave\Config\jobs.json` | Liste des jobs sauvegardés |

---

## 🏗️ Architecture MVVM

```
Utilisateur
    │
    ▼
ConsoleView.cs          ← Affichage uniquement (pas de logique)
    │ appelle
    ▼
BackupViewModel.cs      ← Logique métier, orchestration
    │ utilise
    ├──▶ ConfigService.cs    → Sauvegarde des jobs (jobs.json)
    ├──▶ StateService.cs     → Suivi d'état (state.json)
    └──▶ BackupService.cs    → Copie des fichiers
                │ utilise
                └──▶ Logger.cs (EasyLog DLL) → Logs JSON journaliers
```

---

## 💻 Lancer en ligne de commande (mode CLI)

En plus du mode interactif, l'application supporte un mode CLI direct :

```powershell
# Exécuter le job n°1
.\EasySave.exe 1

# Exécuter les jobs 1 à 3 (plage)
.\EasySave.exe 1-3

# Exécuter les jobs 1 et 3 (sélection)
.\EasySave.exe 1;3
```

---

## 📦 Technologies utilisées

| Technologie | Version | Usage |
|-------------|---------|-------|
| C# | 12.0 | Langage principal |
| .NET | 8.0 | Framework |
| System.Text.Json | Intégré | Sérialisation JSON |
| PlantUML | — | Diagrammes UML |

---

## 👥 Équipe de développement

Projet développé par l'équipe de développement CESI pour le compte de **ProSoft**.

---

*EasySave v1.0 — © ProSoft*
