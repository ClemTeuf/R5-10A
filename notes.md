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
