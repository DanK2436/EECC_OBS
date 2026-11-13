# EECC Caméra

Téléphone Android relié à EECC Studio natif sur le réseau local.

## Parcours

1. Studio affiche un QR à usage unique (3 minutes) avec l'adresse privée détectée, le port 8089 et un jeton aléatoire.
2. **Scanner le QR Code EECC Studio**. L'application explique la demande d'autorisation caméra, puis demande de confirmer le Studio.
3. Après l'accusé `handshake_ack`, les photos JPEG du capteur partent sur `ws://adresse:8089/camera/control`.
4. Le nom reçu (`Cam-1` … `Cam-15`) identifie ce téléphone dans la régie.
5. Une coupure réseau retente la session quelques fois. **Reconnecter** reprend la même place tant que les 45 secondes de grâce ne sont pas passées. Un refus (jeton expiré, déjà utilisé, 16e téléphone) est affiché et n'est pas retenté.

**Coller le code copié dans Studio** utilise le même JSON que le QR. Aucune adresse, aucun jeton et aucun mode démo ne sont proposés.

Le niveau audio annoncé à la régie, le tally et la torche restent sur ce WebSocket. Il n'y a pas de flux audio séparé.

## Limites

Le même Wi-Fi ne suffit pas : pare-feu, isolation des clients, réseau invité ou VPN peuvent bloquer la liaison. Android autorise ici le HTTP clair vers le LAN (`usesCleartextTraffic`), parce que le protocole existant est `ws://`.

Le dossier `ios/` n'existe pas dans ce dépôt. Aucune application iOS n'est livrée par ce changement.

## Tests

`flutter test` vérifie le refus des jetons invalides, expirés ou de démonstration, et la politique de reconnexion. Il ne filme pas un capteur et ne mesure pas 15 téléphones.
