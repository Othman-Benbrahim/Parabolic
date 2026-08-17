# Privacy Policy for Parabolic Download Manager

Effective date: August 18, 2026

Parabolic Download Manager is a Firefox extension that detects downloadable media in web pages and sends user-requested download operations to the Parabolic desktop application through Firefox Native Messaging.

## Data processed

The extension may process the following information in the browser:

- the URL and title of the current page;
- detected media URLs, HLS/DASH manifest URLs, the browser User-Agent, and basic media information such as content type;
- the download preset, quality, format, and priority selected by the user;
- an optional scheduled start time and bandwidth limit;
- the chosen network strategy, proxy mode, authentication mode, and optional page-referrer preference;
- the address and authentication choice for a Cobalt instance configured by the user;
- the status and identifier of a download;
- extension preferences stored locally in Firefox.

Media detection happens locally in Firefox. Detected media candidates are kept in memory and are not sent to the developer.

## Native Messaging and transmission

When the user explicitly asks to list formats, start or cancel a download, or open its destination folder, the extension sends only the information needed to complete that command to the Parabolic application installed on the same computer. Depending on the command, this can include the page URL, media URL, page title, selected format, download identifier, and the internal tab identifier used to return progress to the correct Firefox tab.

The extension also exchanges its extension version and Native Messaging protocol version with the local Parabolic host to verify compatibility. This compatibility exchange does not include browsing history or page content.

To analyze and download the requested media, Parabolic and its download components, including yt-dlp and N_m3u8DL-RE, connect to the website or media provider selected by the user. N_m3u8DL-RE is used only as a fallback for an HLS or DASH manifest already detected by Firefox. When the user requests a yt-dlp update check or update, Parabolic contacts the relevant official update source.

If the user explicitly configures Cobalt, the requested page URL and selected quality are sent to that Cobalt instance to obtain a downloadable media URL. If authentication is configured, the token is sent only to that endpoint. Parabolic does not automatically use the official shared Cobalt API.

If the user explicitly selects `Use Firefox session`, the local Parabolic process invokes yt-dlp's Firefox-cookie support. The extension does not read, copy, receive, or transmit the cookie values. If page-referrer forwarding is enabled, the requested page URL is sent as the HTTP `Referer` header only to the selected media provider. For a browser-detected HLS/DASH fallback, Parabolic automatically sends that same page URL as the stream `Referer` and the Firefox User-Agent because many media CDNs require the browser request context. No cookie header is captured or forwarded to N_m3u8DL-RE.

## Purpose and legal basis

This information is processed only to provide the extension's requested functions: detecting media, retrieving available formats, starting and controlling downloads, reporting progress, and updating yt-dlp at the user's request. Page and media information is sent to Parabolic only as a direct consequence of a download-related action by the user.

## Storage and retention

The extension stores its settings locally using Firefox storage. It does not create a remote browsing history or upload extension settings to the developer.

The optional Cobalt token is stored in Firefox local storage rather than synchronized storage. It is passed transiently to the local Parabolic host for the Cobalt request and is not written to Parabolic's SQLite recovery queue or logs.

Parabolic stores the active browser queue and recovery information locally in SQLite so accepted downloads can continue after Firefox closes and resume after interruption. It may also store download history according to the application's settings. Downloaded and partial files remain on the user's computer until completed or removed. Users can clear download history through Parabolic and can remove extension preferences by uninstalling the extension or clearing its local data in Firefox.

## Sharing, sale, advertising, and analytics

The extension does not sell personal data. It contains no advertising, analytics, behavioral tracking, telemetry service, or advertising identifiers. It does not transmit cookies, passwords, authentication tokens, or complete browsing history to the developer. A Cobalt authentication token is transmitted only to the Cobalt instance expressly configured by the user.

Data necessary to retrieve a requested media file is communicated only to the local Parabolic application and to the websites or media providers involved in fulfilling that request.

## Private browsing

The extension does not run in Firefox private browsing windows. Its manifest declares `"incognito": "not_allowed"`.

## Security

Firefox Native Messaging restricts communication to the registered Parabolic host and the declared Firefox extension identifier. No remotely hosted JavaScript is loaded or executed by the extension.

## Changes to this policy

This policy may be updated when the extension's functionality or data practices change. Material changes will be reflected in the extension's release information and in the effective date above.

## Contact and source code

Source code, releases, and issue reporting:

https://github.com/Othman-Benbrahim/Parabolic

Required Parabolic desktop release:

https://github.com/Othman-Benbrahim/Parabolic/releases/latest

---

# Politique de confidentialité de Parabolic Download Manager

Date d'entrée en vigueur : 18 août 2026

Parabolic Download Manager est une extension Firefox qui détecte les médias téléchargeables dans les pages Web et transmet les téléchargements demandés par l'utilisateur à l'application de bureau Parabolic au moyen de Firefox Native Messaging.

## Données traitées

L'extension peut traiter les informations suivantes dans le navigateur :

- l'adresse et le titre de la page courante ;
- les adresses des médias et manifestes HLS/DASH détectés, l'agent utilisateur de Firefox et certaines informations techniques, comme le type de contenu ;
- le préréglage, la qualité, le format et la priorité choisis par l'utilisateur ;
- une éventuelle date de démarrage programmée et une limite de bande passante ;
- la stratégie réseau, le mode proxy, le mode d'authentification et l'éventuelle autorisation d'envoyer la page comme référent HTTP ;
- l'adresse et le mode d'authentification d'une instance Cobalt configurée par l'utilisateur ;
- l'état et l'identifiant d'un téléchargement ;
- les préférences de l'extension enregistrées localement dans Firefox.

La détection des médias est effectuée localement dans Firefox. Les médias détectés sont conservés en mémoire et ne sont pas envoyés au développeur.

## Native Messaging et transmission

Lorsque l'utilisateur demande explicitement d'afficher les formats, de démarrer ou d'annuler un téléchargement, ou d'ouvrir son dossier de destination, l'extension transmet uniquement les informations nécessaires à l'application Parabolic installée sur le même ordinateur. Selon la commande, celles-ci peuvent comprendre l'adresse de la page, l'adresse du média, le titre de la page, le format choisi, l'identifiant du téléchargement et l'identifiant interne de l'onglet servant à renvoyer la progression au bon onglet Firefox.

L'extension échange également sa version et la version du protocole Native Messaging avec le pont Parabolic local afin de vérifier leur compatibilité. Cet échange de compatibilité ne contient ni historique de navigation ni contenu de page.

Pour analyser et télécharger le média demandé, Parabolic et ses composants de téléchargement, notamment yt-dlp et N_m3u8DL-RE, se connectent au site ou au fournisseur de médias choisi par l'utilisateur. N_m3u8DL-RE n'est utilisé qu'en solution de secours pour un manifeste HLS ou DASH déjà détecté par Firefox. Lorsque l'utilisateur demande une vérification ou une mise à jour de yt-dlp, Parabolic contacte la source officielle correspondante.

Si l'utilisateur configure explicitement Cobalt, l'adresse de la page demandée et la qualité choisie sont transmises à cette instance Cobalt afin d'obtenir une adresse de média téléchargeable. Si une authentification est configurée, le jeton est envoyé uniquement à ce point d'accès. Parabolic n'utilise pas automatiquement l'API Cobalt partagée officielle.

Si l'utilisateur choisit explicitement « Utiliser la session Firefox », le processus Parabolic local utilise la prise en charge des cookies Firefox de yt-dlp. L'extension ne lit, ne copie, ne reçoit et ne transmet jamais les valeurs des cookies. Si l'envoi du référent HTTP est activé, l'adresse de la page demandée est envoyée comme en-tête `Referer` uniquement au fournisseur du média sélectionné. Pour un secours HLS/DASH détecté dans le navigateur, Parabolic transmet automatiquement cette même page comme `Referer` ainsi que l'agent utilisateur de Firefox, car de nombreux CDN exigent ce contexte de requête. Aucun en-tête de cookie n'est capturé ni transmis à N_m3u8DL-RE.

## Finalité du traitement

Ces informations sont utilisées uniquement pour fournir les fonctions demandées : détection des médias, récupération des formats disponibles, démarrage et contrôle des téléchargements, affichage de la progression et mise à jour de yt-dlp à la demande. Les informations relatives à une page ou à un média sont envoyées à Parabolic uniquement à la suite d'une action de téléchargement explicite de l'utilisateur.

## Stockage et conservation

L'extension enregistre ses paramètres localement au moyen du stockage de Firefox. Elle ne crée pas d'historique de navigation distant et ne transmet pas ses paramètres au développeur.

Le jeton Cobalt facultatif est conservé dans le stockage local de Firefox, et non dans le stockage synchronisé. Il est transmis temporairement au pont Parabolic local pour la requête Cobalt et n'est écrit ni dans la file de reprise SQLite de Parabolic ni dans ses journaux.

Parabolic conserve localement dans SQLite la file active du navigateur et les informations de reprise afin que les téléchargements acceptés continuent après la fermeture de Firefox et reprennent après une interruption. L'application peut également conserver un historique conformément à ses paramètres. Les fichiers téléchargés ou partiels restent sur l'ordinateur jusqu'à leur achèvement ou leur suppression. L'utilisateur peut effacer l'historique dans Parabolic et supprimer les préférences de l'extension en la désinstallant ou en effaçant ses données locales dans Firefox.

## Partage, vente, publicité et statistiques

L'extension ne vend aucune donnée personnelle. Elle ne contient ni publicité, ni analyse d'audience, ni suivi comportemental, ni service de télémétrie, ni identifiant publicitaire. Elle ne transmet au développeur ni cookies, ni mots de passe, ni jetons d'authentification, ni historique de navigation complet. Un jeton d'authentification Cobalt est transmis uniquement à l'instance Cobalt expressément configurée par l'utilisateur.

Les données nécessaires à la récupération d'un média demandé sont communiquées uniquement à l'application Parabolic locale et aux sites ou fournisseurs de médias nécessaires à l'exécution de la demande.

## Navigation privée

L'extension ne fonctionne pas dans les fenêtres de navigation privée de Firefox. Son manifeste déclare `"incognito": "not_allowed"`.

## Sécurité

Firefox Native Messaging limite la communication au pont Parabolic enregistré et à l'identifiant déclaré de l'extension Firefox. Aucun code JavaScript distant n'est chargé ou exécuté par l'extension.

## Modification de cette politique

Cette politique peut être mise à jour si les fonctionnalités ou les pratiques de traitement des données évoluent. Toute modification importante sera indiquée dans les informations de publication de l'extension et dans la date d'entrée en vigueur ci-dessus.

## Contact et code source

Code source, versions et signalement de problèmes :

https://github.com/Othman-Benbrahim/Parabolic

Version requise de l'application Parabolic :

https://github.com/Othman-Benbrahim/Parabolic/releases/latest
