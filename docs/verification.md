# Vérifier Sélio sur Windows 11

## Essais reproductibles

Exécutez les tests unitaires et publiez l’exécutable autonome depuis la racine du dépôt.

```powershell
.\scripts\test.ps1
.\scripts\publish.ps1
$exe = (Resolve-Path '.\artifacts\self-contained\Selio.exe').Path
$rapport = Join-Path $PWD 'artifacts\verification-windows.json'
$p = Start-Process -FilePath $exe -ArgumentList ('--verify "' + $rapport + '"') -PassThru -Wait -WindowStyle Hidden
$p.ExitCode
Get-Content -LiteralPath $rapport
```

Ajoutez `--allow-clipboard-tests` à la fin de la chaîne d’arguments pour vérifier également le texte Unicode et l’image après libération du bitmap. Cette option remplace le contenu du presse-papiers. Le rapport contient les résultats et les durées. Un test sans module OCR correspondant est signalé avec `passed: null`.

Les essais de référence sur Windows 11 (build 26200, écran 1920 × 1080 à 125 %) ont donné 60/60 tests unitaires, puis 11/12 vérifications Windows réussies. Le douzième essai, dédié au moteur anglais, a été ignoré faute de module installé. Avec le moteur français, l’essai de code a reconnu le mot repère `total`, avec des omissions dans les symboles. Le test OCR 8K a exercé la réduction et le remappage.

## Parcours graphiques

Vous pouvez lancer une mire avec `Selio.exe --fixture`. Elle affiche des textes synthétiques en français et en anglais, un petit texte, un fond sombre et du code. Ouvrez Sélio normalement dans un second temps.

| Parcours | Manipulation et résultat attendu |
| --- | --- |
| Démarrage | Lancez `Selio.exe`. L’icône apparaît près de l’horloge. Appuyez sur **Ctrl + Maj + O**. Seule la petite popup apparaît. |
| Fermeture | Pressez **Échap**, cliquez à côté puis essayez une seconde pression du raccourci lors de trois ouvertures distinctes. La popup disparaît chaque fois. |
| Texte par mots | Choisissez **Sélectionner du texte**, cliquez sur `Bonjour`, puis recommencez en glissant sur deux lignes. Vérifiez les contours, l’ordre des mots, l’aperçu et **Ctrl + C**. |
| Rectangle OCR | Encadrez deux lignes de la mire avec **Extraire le texte d’une zone**. Vérifiez la ponctuation et les accents. Recommencez avec une zone blanche, puis avec un rectangle de moins de 4 × 4 pixels. La zone blanche doit donner un état vide et le petit rectangle permettre de réessayer. |
| Rectangle image | Encadrez une zone avec **Copier une zone en image** et collez dans Paint. Vérifiez les dimensions et l’absence de la popup dans l’image. |
| Transparence et annulation | Comparez le bureau avant et pendant le glisser. Seul le contour doit être visible. Pendant le glisser, pressez **Échap** et vérifiez que l’application en arrière-plan reçoit à nouveau les clics. |
| Multiécran et DPI | Placez un moniteur à gauche de l’écran principal et utilisez deux échelles, par exemple 100 % et 150 %. Refaites les trois modes sur chaque écran et comparez la position des contours à celle du contenu. |
| Réglages | Essayez un raccourci occupé. L’ancien doit fonctionner. Enregistrez un raccourci libre, relancez Sélio puis vérifiez qu’il est conservé. Désactivez et réactivez le démarrage avec Windows et vérifiez après reconnexion. |
| Reprise après échec | Avec un utilitaire de test qui retient le presse-papiers quelques secondes, essayez une copie. Vérifiez le message puis réessayez après libération. Vérifiez aussi le comportement sur une zone protégée autorisée à tester. |
| Sortie | Dans les réglages ou le menu de notification, cliquez sur **Quitter Sélio**. Vérifiez la disparition de l’icône et la libération du raccourci. |

La popup, ses quatre libellés, le raccourci, l’absence d’assombrissement et l’annulation de la sélection ont été observés sur l’écran de test. Le glisser réel sur la surface transparente, le multiécran physique, le démarrage à la reconnexion et le test d’un presse-papiers occupé restent à vérifier avec ce protocole.

## Mesures de ressources

Fermez Sélio et lancez `scripts/benchmark.ps1` sur un bureau Windows déverrouillé. Le script enregistre la mémoire résidente (*Working Set*), la mémoire privée (*Private Bytes*) et le CPU du processus, normalisé par le nombre de processeurs logiques. Les cycles distinguent la latence d’affichage, le temps OCR et les objets GDI et USER. Le rectangle est fourni par le banc de test. La comparaison des paliers après 20 et 50 cycles permet de repérer une dérive éventuelle, en tenant compte du JIT et du cache OCR.
