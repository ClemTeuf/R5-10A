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
