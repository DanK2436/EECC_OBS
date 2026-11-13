# EECC Studio natif — téléphones sur le réseau local

La régie Windows qui parle à EECC Caméra est `apps/studio_native`. Le transport déjà présent est conservé : WebSocket sur le port **8089**, chemin `/camera/control`, images JPEG binaires, télémétrie, tally et commandes texte. L'interface Electron de `apps/studio` n'écoute pas le réseau.

## Appairer un téléphone

1. Lancer EECC Studio. Le serveur doit indiquer qu'il écoute. S'il ne peut pas ouvrir le port, le message le dit.
2. Ouvrir **Connecter un téléphone**.
3. Choisir l'adresse IPv4 privée réellement détectée. S'il n'y en a aucune, aucun QR n'est affiché et aucune adresse n'est inventée.
4. Sur le téléphone, ouvrir EECC Caméra, scanner le QR et confirmer **Se connecter à EECC Studio**.
5. Le téléphone envoie ses images seulement après `handshake_ack`. Dans Studio, choisir cette source : l'aperçu montre les JPEG reçus. **Couper** les place à l'antenne.
6. Chaque QR est à usage unique et expire au bout de **3 minutes**. **Renouveler** ou **Annuler** révoque le code encore inutilisé. Après un téléphone accepté, un nouveau QR s'affiche pour le suivant.
7. Un téléphone coupé par le réseau peut reprendre sa place pendant **45 secondes** avec l'identifiant de session reçu à l'appairage. Quitter l'écran caméra libère la place. Ce n'est pas un secret permanent : il n'est pas dans le QR.

Jusqu'à **15 téléphones** en même temps. Le 16e reçoit un refus explicite. Les caméras physiques ajoutées dans les scènes ne prennent pas ces places.

Le niveau audio, la batterie, le tally et la commande `toggle_torch` passent dans le même WebSocket. Il n'y a pas de piste audio séparée : le micro du téléphone n'est pas mixé dans le programme.

## Ce que « le même Wi-Fi » ne garantit pas

- Le pare-feu Windows peut bloquer le port 8089 en entrée.
- Un réseau invité ou une isolation des clients empêche le téléphone de joindre le PC.
- Un VPN sur le téléphone ou le PC sort du réseau local.
- Seules les adresses IPv4 privées (10/8, 172.16/12, 192.168/16) sont proposées.
- La vidéo circule en `ws://` clair sur le LAN. Le jeton limite qui peut s'appairer ; il ne chiffre pas les images.
- Le projet iOS n'est pas dans ce dépôt (pas de dossier `ios/`). Le code Flutter est partagé, mais aucun projet Xcode n'est généré ici.

## Tests

`dotnet test apps/studio_native/tests/EeccStudio.Core.Tests/EeccStudio.Core.Tests.csproj`

Les clients sont des WebSocket synthétiques, marqués `SYNTHETIC_TEST_CLIENT`. Ils ne mesurent pas 15 flux réels et ne remplacent pas un téléphone.
