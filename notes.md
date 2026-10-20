Etape 1 - Lire avant de commencer
Q1 : Le fichier déclare les quatre services qui seront utilisés au cours des différents TP. Aujourd'hui, on se concentre sur PostgreSQL, et les autres services qui sont déjà configurés seront exploités plus tard.
Q2 : Parmi les quatre services, redis ne possède pas de section volumes, ce qui mène à une perte des données lorsque le conteneur est supprimé, car à l'inverse les services contenant une section volumes conservent leurs données en dehors du conteneur.
Q3 : "15432:5432" possède deux parties : 15432 correspond au port utilisé sur la machine de développement, pendant que 5432 correspond au port utilisé par PostgreSQL dans le conteneur.
Q4 : Dans le cadre du TP, garder les mots de passe en clair est acceptable car il s'agit d'un environnement local, cependant en production, ce n'est pas tolérable.

Etape 2 - Démarrer les quatre moteurs
Q5 : La commande docker compose exec permet d'exécuter une commande directement à l'intérieur d'un conteneur Docker déjà démarré. La commande redis-cli s'exécute à l'intérieur du conteneur Redis pixelhub-redis.

Etape 3 — L'application PixelHub
Q6 : L'API .NET s'exécute directement sur la machine Windows, et non dans le conteneur PostgreSQL. Dans le docker-compose.yml, on a 15432:5432, donc l'application se connecte à localhost:15432 sur Windows, puis Docker redirige la connection vers le port 5432 du conteneur PostgreSQL.
Q7 : Les joueurs ne sont pas dupliqués car le programme vérifie au démarrage la présence de joueurs dans la table, et les joueurs sont ajoutés seulement si la table est vide. Les données existent toujours après l'arrêt et le redémarrage de l'application.

TP 2 — Le catalogue de jeux dans MongoDB

Partie 1 — MongoDB sans C#
1.3 Exercices

1 : Il y a 10 jeux dans la collection.
2 : pixelhub> db.jeux.findOne()
{
\_id: ObjectId('6ac38ef8fbfc30ec02e8af92'),
titre: 'Counter-Strike 2',
genre: 'FPS',
note: 4.5,
anneeSortie: 2023,
plateformes: [ 'PC' ],
tags: [ 'compétitif', 'tir', 'équipe' ],
joueursParEquipe: 5,
cartes: [ 'Dust II', 'Mirage', 'Inferno', 'Nuke' ],
classementCompetitif: true
}
Q1 : Le champ \_id est créé automatiquement par mongoimport, et non par le serveur MongoDB. Lors de l'importation, mongoimport ajoute un identifiant \_id aux documents qui n'en possèdent pas.

Q2. En SQL, avec un modèle relationnel normalisé, les plateformes pourraient être stockées dans des tables séparées avec une liaison entre les jeux et les plateformes, il faut donc utiliser des JOIN pour retrouver les jeux disponibles sur Switch.

Q3. Le compte pixelhub a été créé dans la base admin, et non dans la base pixelhub. --authenticationDatabase admin indique à MongoDB dans quelle base se trouve le compte pour s'authentifier. Ensuite, on peut accéder à la base pixelhub.

Q4. L'avantage est que MongoDB est flexible : il n'impose pas de schéma fixe et permet d'ajouter facilement de nouveaux champs. Cependant, en prenant l'exemple d'un projet à quatre développeurs six mois plus tard, chacun d'entre eux pourrait ajouter des champs différents ou utiliser des noms différents pour la même chose, ce qui mène à des structures incohérentes.

Q5. Dans l'exercice 9, on a écrit Jeux avec une majuscule, et non jeux avec une minuscule : MongoDB considère Jeux et jeux comme deux collections différentes, et Jeux n'existant pas, MongoDB a retourné un résultat vide, ce qui correspond à la flexibilité mentionné dans la question précédente.

Q6. pixelhub> db.jeux.updateOne({ titre: "Valorant" }, { note: 4.4 })
MongoInvalidArgumentError: Update document requires atomic operators
MongoDB refuse cette commande car updateOne() attend un document de mise à jour avec un opérateur de mise à jour tel que $set, $inc ou encore $push. La commande de l'exercice 16 ne contient aucun opérateur, MongoDB ne sait donc pas quoi faire avec la commande, et retourne une erreur. Si l'on voulait remplacer le document entier, on devrait utiliser la commande replaceOne()

Q7. En SQL, il aurait fallu modifier la structure de la table avec un ALTER TABLE, par exemple ALTER TABLE jeux ADD COLUMN nbVotes int;

Partie 2 — MongoDB dans PixelHub

Q8. "Mongo" n'apparaît nulle part dans IGameCatalog.cs. Cette interface définit les opérations nécessaires pour accéder au catalogue sans préciser comment les données sont stockées. C'est important car cela permet de séparer l'interface et l'implémentation : le reste de l'application dépend de IGameCatalog, et non de Mongo, permettant alors d'utiliser une autre implémentation.

Q9. L'exception obtenue concerne le champ \_id, correspondant à la propriété Id de la classe Game. Le driver MongoDB essaie de désérialiser la valeur \_id présente dans les documents MongoDB en un ObjectId, car notre modèle contient
[BsonId]
public ObjectId Id { get; set; }
Le problème est que certains documents importés contiennent un \_id qui n'est pas compatible avec le type ObjectId attendu par le modèle C#. Le driver ne sait donc pas convertir cette valeur en ObjectId, ce qui provoque l'exception. Il essaie donc de faire correspondre automatiquement le document BSON de MongoDB avec notre classe C#, mais le type réel de \_id ne correspond pas au type déclaré dans Game.

Q10. [BsonIgnoreExtraElements] permet de ne plus provoquer d'erreur lorsque MongoDB contient des champs qui n'existent pas dans la classe Game, mais ces champs sont ignorés par le driver et ne sont pas récupérés dans l'objet C# : par exemple, le champ joueursParEquipe présent dans certains documents mais absent de la classe Game est ignoré lors de la désérialisation, il n'apparaît donc pas dans la réponse de /games.

Q11. [BsonIgnoreExtraElements] :

- avantage : simple et évite les erreurs
- inconvénient : on perd l'accès aux données supplémentaires
  [BsonExtraElements] :
- avantage : on conserve les données variables sans avoir à créer une propriété C# pour chaque champ
- inconvénient : on perd une partie du typage fort de C# et il faut gérer ces données supplémentaires manuellement
  Hiérarchie de classes comme FpsGame : Game :
- avantage : on conserve un modèle fortement typé, ce qui rend le code plus clair et facilite l'utilisation des propriétés spécifiques
- inconvénient : cela devient plus complexe à maintenir si les types de jeux sont nombreux ou si le schéma évolue souvent

Partie 3 — Une agrégation

Q12. La requête SQL correspondante serait :

```SQL
SELECT genre, AVG(note) AS note_moyenne, COUNT(\*) AS nombre
FROM jeux
GROUP BY genre
ORDER BY note_moyenne DESC;
```

Les deux requêtes font donc exactement le même travail : elles regroupent les jeux par genre, calculent la note moyenne et le nombre de jeux, puis trient les résultats par note moyenne décroissante. Ce n'est pas sur cette agrégation que MongoDB apporte un avantage particulier par rapport à PostgreSQL qui est tout à fait capable de réaliser ce type de requête efficacement : l'intérêt de MongoDB dans ce TP se situe plutôt dans sa souplesse de modèle : les documents peuvent avoir des champs différents, des tableaux comme plateformes et tags, ou des champs spécifiques comme joueursParEquipe sans modifier la structure de toute la collection.

TP 3 — Classements, matchmaking et cache avec Redis

Partie 1 — Les structures dans redis-cli

Q1. INCR est une opération atomique, cela veut dire que Redis effectue l'incrémentation en une seule opération. Par exemple, si 1000 joueurs terminent une partie en même temps, chaque commande INCR compteur:parties est traitée sans qu'aucune autre commande puisse venir modifier la valeur entre la lecture et l'écriture.
En SQL, on pourrait faire :

```SQL
UPDATE compteur
SET valeur = valeur + 1
WHERE nom = 'parties';
```

Q2. Si on avait stocké tout le joueur en JSON dans un String Redis, pour ajouter 250 XP, il aurait fallu :

- Faire un GET joueur:7 pour récupérer le JSON
- Désérialiser le JSON en objet
- Modifier xp : 3400 + 250 = 3650
- Sérialiser l'objet en JSON
- Faire un SET joueur:7 avec le nouveau JSON
  Cependant, les deux opérations ne sont pas atomiques, avec les deux opérations on devrait obtenir 3900, mais la deuxième opération écraserait la première.

Q3. ZREVRANGE classement:global 0 2 WITHSCORES -> renvoie les joueurs avec les scores les plus élevés en premier
ZRANGE classement:global 0 2 WITHSCORES -> renvoie les joueurs avec les scores les plus faibles en premier
Pour afficher un "top 3", on utiliserait : ZREVRANGE classement:global 0 2 WITHSCORES

Q4. ZREVRANK renvoie renvoie un rang commençant par 0. Si on l'affiche ce nombre tel quel au joueur, il va croire qu'il est le "0ème", il faut donc ajouter +1 au résultat de ZREVRANK.

Q5. Au RPOP, Nova sort car il est en bout de liste. À l'inverse, au LPOP, Ombre sort car il est en tête de liste.

Q6. Au départ, on obtient un chiffre entre 0 et 30 en première ligne, et 1 sur la seconde. Sans expiration, TTL renvoie -1. Résultat : un chiffre nul ou positif correspond au nombre de secondes avant expiration, -1 correspond à une clé sans expiration, et -2 correspond à une clé inexistante ou expirée.

Q7. Avec PostgreSQL, il aurait fallu stocker la date d'expiration dans une table, puis mettre en place dans une tâche périodique pour supprimer les joueurs dont la présence a expiré.

```SQL
CREATE TABLE joueurs_en_ligne (
    pseudo VARCHAR(50) PRIMARY KEY,
    expire_le TIMESTAMP NOT NULL
);

-- date d'expiration
INSERT INTO joueurs_en_ligne (pseudo, expire_le)
VALUES ('Nova', NOW() + INTERVAL '30 seconds');

-- nettoyage
DELETE FROM joueurs_en_ligne
WHERE expire_le <= NOW();
```

Ce code introduit plusieurs risques :

- nettoyage en retard, si un joueur expire avant le nettoyage, on ne le sait pas
- charge supplémentaire, on interroge régulièrement la BDD et supprime les lignes expirées

Partie 2 — Redis dans PixelHub

Q8. Le ConnectionMultiplexer est l'objet principal de StackExchange.Redis qui gère les connexions entre l'application C# et le serveur Redis. Il est conçu pour être partagé et réutilisé pendant toute la durée de vie de l'application.
Si on en créait un à chaque requête HTTP, cela entraînerait plusieurs problèmes :

- des connexions inutiles : chaque requête créerait de nouvelles connexions à Redis
- des performances réduites : la création répétée des connexions consommerait du temps et des ressources
- du risque de saturation : le nombre de connexions et de sockets pourrait devenir beaucoup trop important
  Le bug serait invisible car on fait peu de requêtes dans le TP.

Q9. Si on supprime Order.Descending, Redis renvoie les joueurs du score le plus faible au score le plus élevé, au lieu d'afficher les meilleurs joueurs en premier. En plus d'être visiblement faux, ce bug est particulièrement dangereux car aucune erreur n'est affichée et les scores restent corrects dans Redis.

Q12.

Partie 3 — Mesurer, et se méfier de la mesure

Q13. Lors du premier appel à /games/timed, il y a un cache miss : les données sont récupérées depuis MongoDB, puis enregistrées dans Redis pendant 60 secondes. Le temps de réponse devrait donc être plus élevé.
Lors des appels suivants, il y a des cache hits : les données sont directement récupérées depuis Redis, ce qui devrait réduire le temps de réponse. Après 60 secondes, le cache expire et un nouvel appel doit récupérer les données depuis MongoDB.
Le gain n'est cependant pas spectaculaire : dans le TP, le catalogue contient peu de jeux et MongoDB fonctionne en local.
Le cache Redis permet d'éviter des lectures répétées dans MongoDB et peut accélérer les réponses. Cependant, un test sur un petit catalogue en local ne suffit pas à démontrer un gain important en conditions réelles. Il devient réellement décisif lorsque :

- le nombre de requêtes simultanées est élevé
- les données sont coûteuses à récupérer ou à calculer
- les mêmes données sont consultées fréquemment

Q14. Les joueurs pourront voir l'ancien titre pendant 60 secondes maximum, car le cache Redis a un TTL de 60 secondes. Lorsqu'un administrateur modifie directement le titre dans MongoDB, la copie présente dans Redis n'est pas automatiquement mise à jour. Elle reste disponible jusqu'à son expiration.
Pour réduire ce délai, on peut utiliser deux stratégies :

- réduire la durée du TTL, par exemple à 10 secondes, afin d'actualiser plus souvent, mais cela sollicite plus souvent MongoDB
- invalider le cache lors d'une modification, en supprimant la clé cache:catalogue:tous après la mise à jour du jeu

TP4 - Neo4j, puis PixelHub à quatre moteurs

Partie 1 — Neo4j

Q1. Si on lançait le script deux fois avec CREATE, Neo4j créerait à nouveau les mêmes nœuds et relations, on aurait donc des doublons dans le graphe. Au contraire, MERGE cherche si le nœud existe déjà et ne le recrée pas s'il existe.
Les trois CREATE CONSTRAINT servent à garantir l'unicité de certains identifiants dans le graphe. Elles empêchent notamment d'avoir deux nœuds représentant le même joueur, le même jeu ou la même guilde avec le même identifiant.

Q2. Sans flèche → 2 résultats
Avec -> → 1 résultat
Sans flèche, on demande les relations AMI_DE dans n'importe quelle direction. On récupère donc les deux joueurs reliés à Krayz, peu importe le sens de la relation.
Avec flèche, on demande uniquement les relations qui partent de Krayz. Il n'y en a qu'une.
Ici, si AMI_DE représente une relation d'amitié qui devrait être réciproque, le modèle pourrait être considéré comme incomplet si une seule direction est enregistrée, mais la requête avec -> est correcte par rapport aux données réellement présentes : elle respecte la direction enregistrée dans Neo4j.
Il n'y a pas d'erreur envoyée car Neo4j considère une relation comme orientée par nature, mais il ne sait pas que AMI_DE est censée être réciproque.

Q3. Avec count(DISTINCT ami), on compte le nombre d'amis différents de Nova qui possèdent le jeu.
Avec count(*), on compte le nombre de chemins/résultats trouvés, donc un même ami peut être compté plusieurs fois s'il existe plusieurs chemins permettant d'arriver jusqu'à lui.
Pour passer de *1..2 à \*1..4, on change une seule ligne dans la requête. En SQL, il faudrait généralement écrire plusieurs niveaux de jointures/CTE pour parcourir successivement les relations :
Nova → amis
Nova → amis d'amis
Nova → niveau 3
Nova → niveau 4

Q4. Stardew Valley n'apparaît jamais parce que Nova possède déjà ce jeu.

Q5. Parce que Neo4j utilise deux ports pour deux services différents :

- 17474 → port de l'interface web accessible depuis le navigateur
- 17687 → port utilisé par l'application pour communiquer avec Neo4j

Q6. Les paramètres $pseudo protègent les valeurs. La profondeur doit être interpolée car elle fait partie de la syntaxe du chemin. Comme elle est interpolée, il faut impérativement la valider/born­er avant. Ici : 1 ≤ profondeur ≤ 4.

Q7. Le IDriver est un singleton parce qu'il est conçu pour être partagé par toute l'application. Il gère les connexions à Neo4j et évite de recréer un driver à chaque requête.
La session est créée dans chaque méthode avec AsyncSession(), puis libérée automatiquement grâce à await using à la fin de l'opération.
Cela permet de ne pas conserver inutilement des sessions ouvertes et de gérer correctement les ressources.

Partie 2 — PixelHub ouvre sa boutique

Q8. Les guillemets doubles servent à respecter la casse des identifiants PostgreSQL. Sans guillemets, les noms sont convertis en minuscules.

Q9. UNIQUE ("PlayerId", "Titre") garantit qu'un même joueur ne peut acheter qu'une seule fois un jeu donné dans la table Purchases.
CHECK ("Coins" >= 0) garantit que le solde d'un joueur ne peut jamais être négatif.
UPDATE "Players" SET "Coins" = -1 WHERE "Pseudo" = 'Ombre'; PostgreSQL refuse la modification et renvoie une erreur de contrainte CHECK car -1 est inférieur à 0.

Q10. La première commande fixe le prix de tous les jeux à 800 (11 modifiedCount), la deuxième fixe le prix des jeux ayant une supérieure ou égale à 4,7 à 1200 (4 modifiedCount), et la troisième fixe le prix des jeux de genre FPS à 0 (2 modifiedCount). modifiedCount indique le nombre de documents réellement modifiés.
Aucun document n'a été réécrit en entier : l'opérateur $set modifie uniquement le champ prix, en conservant les autres champs du document.
En SQL, on aurait utilisé plusieurs requêtes UPDATE :
UPDATE Jeux SET prix = 800;
UPDATE Jeux SET prix = 1200 WHERE note >= 4.7;
UPDATE Jeux SET prix = 0 WHERE genre = 'FPS';
Si on lançait la première commande en dernier, tous les jeux auraient finalement un prix de 800, car la première commande écraserait les prix précédemment définis à 1200 et à 0.

Q11. La valeur en cache ne contient pas encore les prix : la classe Game de l'API ne contient pas le champ prix, donc la réponse mise en cache ne peut pas le renvoyer, même si MongoDB contient désormais les prix.
L'API servira un catalogue sans prix au maximum 60 secondes.
DEL cache:catalogue:tous force la mise à jour immédiatement.

Q12. 127.0.0.1:6379> ZREVRANGE classement:global 0 2 WITHSCORES

1. "Krayz"
2. "5200"
3. "Ombre"
4. "5100"
5. "Nova"
6. "4500"

   127.0.0.1:6379> KEYS \*

1) "classement:global"
2) "ventes:jeux"
3) "compteur:parties"
4) "joueur:7"
5) "file:matchmaking"

Q13. Luna existe pour Neo4j, mais pas PostgreSQL.
PostgreSQL est la source de vérité pour les joueurs et leur solde. Luna n'existant pas dans PostgreSQL, l'achat doit échouer : : aucune ligne de joueur ne pourra être débitée et la clé étrangère PlayerId de la table Purchases empêchera d'enregistrer un achat qui référence un joueur inexistant.

Q14. ExecuteUpdateAsync permet de modifier directement les données en base de données, sans charger puis modifier l'entité en C#. La requête SQL correspondante est la suivante :
UPDATE "Players"
SET "Coins" = "Coins" - @prix
WHERE "Id" = @playerId
AND "Coins" >= @prix;
Avec une vérification en C#, deux achats simultanés pourraient lire le même solde avant de le modifier. On risque alors un débit incorrect ou un écrasement de mise à jour. Avec ExecuteUpdateAsync, le test du solde et le débit sont effectués dans une seule instruction SQL.

Q15. AnyAsync ne suffit pas, car deux requêtes peuvent s'exécuter simultanément. Si deux requêtes identiques arrivent à la même milliseconde, le code intercepte l'exception UniqueViolation et renvoie DejaPossede. Il ne faut pas faire ce test dans Neo4j, car PostgreSQL est la source de vérité pour les achats et les débits.

Q16. L'achat ne passe pas, car Krayz possède déjà Valorant, et ceci est vérifié dans PostgreSQL, et non Neo4j.
PostgreSQL fait foi pour les achats. La table "Purchases" est la source de vérité : elle conserve la trace des achats réellement effectués et le prix payé.
Les données importées manuellement dans un graphe peuvent représenter un état initial fictif ou préexistant, différent de l'historique réel de la boutique. Il faut donc distinguer PostgreSQL avec les achats réels, débits et historique faisant foi, et Neo4j le graphe des possessions utilisé pour les recommandations mais qui doit être synchronisé avec PostgreSQL.

Mission 3 — La fiche joueur : quatre moteurs, une réponse

Q18. La fiche joueur interroge les 4 moteurs de bases de données pour construire une seule réponse API.
PostgreSQL : 2 requêtes, Informations du joueur et solde, puis nombre d'achats
Redis : 1 requête, Rang du joueur
Neo4j : 2 requêtes, Liste des amis, puis jeux possédés
MongoDB : 1 requête, Fiches complètes des jeux possédés
=> 6 requêtes envoyées aux moteurs
Dans le cas où Nova possédait 200 jeux :

- avec GetByTitlesAsync : 1 requête MongoDB, grâce au filtre $in, qui recherche tous les titres en une seule fois
- avec une boucle de GetByTitleAsync : 200 requêtes MongoDB, une par jeu

Q19. PostgreSQL décide que Luna n'existe pas dans le contexte de la fiche joueur.
Est-ce le bon choix ? Oui, pour cette architecture. La fiche joueur nécessite un compte existant dans PostgreSQL, notamment pour récupérer le solde et le nombre d'achats.

Mission 4 — Casser, constater, réparer

Q20. Le code HTTP obtenu est 500 Internal Server Error.
Ombre n'a pas été débité : il reste les 1000 + les 90 qu'il avait avant, l'achat est dans Purchases, comme le précise la ligne 4 | 3 | Rocket League | 0 | 2026-10-20 11:37:38.288849+00
Il reçoit une erreur de l'API et peut penser que son achat a échoué, alors que l'achat est déjà enregistré dans PostgreSQL. Les données sont donc incohérentes : l'achat existe, mais son prix n'a pas été prélevé.
En ce qui concerne l'attente de 30 secondes, l'application attend probablement l'expiration du délai de connexion à Neo4j ou de plusieurs tentatives de connexion avant de renvoyer l'erreur.

Q21. Quand Ombre réessaie d'acheter Rocket League, l'achat ne devrait pas être enregistré une deuxième fois et il ne devrait pas être débité deux fois. Cela est rendu possible par la vérification de l'existence de l'achat dans la table "Purchases" et par la contrainte d'unicité en base de données.

Q22. L'achat de Rocket League est enregistré dans Purchases avec PrixPaye = 0, alors qu'Ombre n'a pas été débité. L'enregistrement de l'achat est donc incorrect.
La relation entre Ombre et Rocket League est absente, puisque Neo4j était arrêté au moment de l'achat. Le graphe ne reflète alors pas l'achat enregistré dans PostgreSQL.

Q23. MERGE rend la reconstruction Neo4j rejouable sans doublons, tandis que PostgreSQL reste la source de vérité et Redis une projection reconstruisible.

Q24.
SOLDE DE MONNAIE :
Sole de référence : PostgreSQL (Players.Coins)
Projection(s) : Aucune
Se reconstruit à partir de : PostgreSQL
Retard acceptable : Non

« Ombre a acheté Rocket League » :
Sole de référence : PostgreSQL (Purchases)
Projection(s) : Neo4j (POSSEDE)
Se reconstruit à partir de : PostgreSQL (Purchases)
Retard acceptable : Oui, temporairement

Prix courant d'un jeu :
Sole de référence : MongoDB (games)
Projection(s) : Cache Redis (cache:catalogue:tous)
Se reconstruit à partir de : MongoDB
Retard acceptable : Oui, pendant le TTL du cache

Prix payé par Ombre :
Sole de référence : PostgreSQL (Purchases.PrixPaye)
Projection(s) : Aucune
Se reconstruit à partir de : PostgreSQL (historique d'achat)
Retard acceptable : Non

Meilleures ventes :
Sole de référence : PostgreSQL (Purchases)
Projection(s) : Redis (classement:ventes)
Se reconstruit à partir de : PostgreSQL, en comptant les achats par jeu
Retard acceptable : Oui, temporairement

Catalogue complet :
Sole de référence : MongoDB
Projection(s) : Cache Redis (cache:catalogue:tous)
Se reconstruit à partir de : MongoDB
Retard acceptable : Oui, pendant le TTL du cache

Q25. Même si MongoDB fonctionne parfaitement, /games échoue parce que l'endpoint dépend de Redis pour gérer le cache du catalogue. Lorsque Redis est arrêté, l'application tente d'accéder au cache et rencontre une erreur.
Le cache, initialement destiné à améliorer les performances, est donc devenu une dépendance critique : au lieu d'être une simple optimisation, il peut empêcher l'accès au catalogue lorsque Redis est indisponible.

Q26. On peut tolérer une panne de Redis car Redis contient des données secondaires ou temporaires, alors que PostgreSQL contient les données de référence indispensables aux opérations de la boutique.
