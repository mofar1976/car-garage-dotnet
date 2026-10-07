# Car Garage .NET Backend

Schrittweise Umsetzung des Car-Garage-Backends in C# und ASP.NET Core (.NET 10).

Als fachliche und API-seitige Referenz dient `../car-garage/apps/api`, ergänzt um
die API-Typen in `../car-garage/packages/shared`. Das bestehende Backend verwendet
Express und MongoDB; ein Verzeichnis `car-garage-nest` ist in diesem Workspace
nicht vorhanden.

Ziel ist dieselbe Backend-Funktionalität für Kunden, Werkstattmitarbeiter und
Plattformadministratoren. Das bestehende Angular-Frontend soll später über eine
konfigurierbare API-Adresse angebunden werden können. UUID-basierte IDs werden
bei der Anbindung auf Frontend-Kompatibilität geprüft.

Für das neue Backend sind **PostgreSQL** und **Docker/Docker Compose** festgelegt.
Der Datenzugriff erfolgt mit Entity Framework Core und Npgsql; PostGIS
unterstützt die Umkreissuche. **Swagger UI** unter `/swagger` dient zur
Dokumentation und zum interaktiven Testen der API. Die Referenzdatenbank bleibt
unverändert.

Der lokale Implementierungsplan `PLAN.md` wird bewusst nicht versioniert.
Schritt 1 ist implementiert: vier Projekte, Health-Endpunkt, Swagger UI,
validierte Service-Konfiguration, strukturierte Logs, JSON-Fehlerbehandlung,
Dockerfile und Compose mit PostgreSQL/PostGIS.
Schritt 2 ergänzt den DbContext, das User-Modell mit Rollen und Audiences,
validierte Datenbankkonfiguration und die Migration `InitialUsers`.
Schritt 3 ergänzt die Auth-Endpunkte aller drei Portale, bcrypt, JWT, atomare
Refresh-Token-Rotation, Logout, Swagger-Bearer-Auth und die Admin-CLI.
Weitere Fachfunktionen folgen in den nächsten Schritten. `/api/health` prüft aktuell nur die laufende API,
keine Datenbankverbindung.

## Start mit Docker

Docker Desktop muss laufen. Im Verzeichnis `car-garage-dotnet`:

```bash
cp .env.example .env
# POSTGRES_PASSWORD und JWT_ACCESS_SECRET in .env festlegen
# Einen zufälligen JWT-Schlüssel erzeugen: openssl rand -hex 32

docker compose up -d --build --wait
```

- Swagger: http://localhost:4000/swagger
- Health: http://localhost:4000/api/health
- PostgreSQL: `localhost:5433`; Datenbank und Benutzer gemäß `.env`.

Die Ports lassen sich in `.env` anpassen, etwa `API_PORT=4001`, falls das
bestehende Backend bereits Port 4000 verwendet. Im Compose-Netzwerk ist
PostgreSQL unter `postgres:5432` erreichbar. Compose übergibt den Connection
String an die API und einen separaten `migrate`-Service. Dieser wendet die
versionierten Migrationen an und beendet sich; erst danach startet die API.
Bei einem Migrationsfehler startet die API nicht. Der HTTP-Server selbst führt
keine automatischen Migrationen aus.

```bash
docker compose ps
docker compose logs -f api
docker compose down
```

`down` erhält die Daten im Volume. Das PostGIS-Image läuft auf Apple Silicon
über AMD64-Emulation; das API-Image wird für die native Architektur gebaut.
Swagger ist im Compose-Entwicklungsbetrieb aktiviert, in Production deaktiviert.

## Lokale Entwicklung

Voraussetzung: .NET SDK 10. `global.json` erlaubt aktuelle .NET-10-Feature-Bands.
Das ursprünglich installierte SDK 9 reicht für dieses Projekt nicht aus.

```bash
dotnet restore CarGarage.sln
dotnet build CarGarage.sln
dotnet test CarGarage.sln

docker compose up -d postgres
# Werte entsprechend deiner .env einsetzen; dotnet liest .env nicht automatisch.
export Database__ConnectionString='Host=localhost;Port=5433;Database=car_garage;Username=car_garage;Password="DEIN_PASSWORT"'
dotnet run --project src/CarGarage.Api -- --migrate
export Jwt__Secret='DEIN_ZUFAELLIGER_JWT_SCHLUESSEL_MINDESTENS_32_BYTES'
dotnet run --project src/CarGarage.Api
```

Das Startprofil verwendet Port 4000 und die Entwicklungsumgebung. PostgreSQL kann
bei Bedarf separat mit `docker compose up -d postgres` gestartet werden.
Die HTTP-Tests benötigen keine Datenbank. Die Persistenztests starten mit
Testcontainers automatisch einen eigenen PostgreSQL-17-Container mit zufälligem
Port und Passwort und entfernen ihn nach dem Testlauf. Sie verwenden keine
Compose- oder Entwicklungsdatenbank. Docker Desktop muss dafür laufen.
Die Geo-Funktionen und entsprechende PostGIS-Tests folgen in Schritt 6.

```bash
# Nur Tests ohne Docker:
dotnet test CarGarage.sln --filter 'Category!=Database'
```

Bei Docker-Socket-Erkennungsproblemen auf macOS:

```bash
export DOCKER_HOST="unix://$HOME/.docker/run/docker.sock"
export TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock
dotnet test CarGarage.sln
```

`Database__ConnectionString` ist erforderlich; für den HTTP-Server zusätzlich
`Jwt__Secret` mit mindestens 32 UTF-8-Bytes. Die Migration und die Admin-CLI
benötigen keinen JWT-Schlüssel. Fehlende oder syntaktisch ungültige
Konfiguration führt zu einem Startfehler mit einer Meldung ohne Zugangsdaten.
Eine tatsächlich erreichbare Datenbank wird durch Migrationen beziehungsweise
Datenbankzugriffe geprüft; `/api/health` bleibt ein Liveness-Endpunkt.

## Migrationen

Das lokale EF-Tool wird über `.config/dotnet-tools.json` versioniert.
Den Connection String wie oben setzen, bevor die folgenden Befehle laufen:

```bash
dotnet tool restore
dotnet ef migrations add NameDerAenderung --project src/CarGarage.Infrastructure --startup-project src/CarGarage.Infrastructure --output-dir Persistence/Migrations
dotnet ef migrations has-pending-model-changes --project src/CarGarage.Infrastructure --startup-project src/CarGarage.Infrastructure
```

Migration `AddRefreshTokens` ergänzt die Refresh-Token-Tabelle, einen
User-Fremdschlüssel sowie Hash- und Ablaufzeitindizes.

Für die Anwendung bestehender Migrationen reicht der API-Ausführungsmodus
`--migrate`; er funktioniert auch im veröffentlichten Docker-Image ohne EF-Tool.
Der Design-Time-DbContext liest den Connection String ausschließlich aus der
Umgebungsvariable. Migrationen und ModelSnapshot werden gemeinsam versioniert.

## User-Persistenz

Die Tabelle `users` enthält UUID, E-Mail, Passwort-Hash, Namen, Telefon,
Audience, Rollen, Aktiv-/Löschstatus und UTC-Zeitstempel. Rollen sind ein
PostgreSQL-Textarray; Check-Constraints begrenzen Rollen und Audience auf die
bekannten Werte. Die API gibt bisher keine User-Daten aus.

`SaveChanges` und `SaveChangesAsync` normalisieren die E-Mail und trimmen Namen
und Telefon; sie setzen `CreatedAt` und `UpdatedAt`. Bei Updates bleibt das
persistierte Erstellungsdatum erhalten. Ein partieller Unique-Index verhindert
doppelte E-Mails innerhalb derselben Audience für nicht gelöschte Nutzer.
Soft-Delete-Filter werden wie in der Referenz explizit pro Abfrage gesetzt;
es gibt keinen globalen Query-Filter. Ein inaktiver, nicht gelöschter Nutzer
belegt seine E-Mail weiterhin.

## Projektaufbau

- `CarGarage.Api`: HTTP-Controller, Swagger, Konfiguration und Middleware.
- `CarGarage.Application`: Auth-/Registrierungsservices, Modelle und Persistenz-/Token-/Hashing-Schnittstellen.
- `CarGarage.Domain`: User, RefreshToken und Auth-/Rollen-Enums.
- `CarGarage.Infrastructure`: DbContext, AuthStore, Mappings/Migrationen, bcrypt und JWT-Ausstellung.
- `CarGarage.IntegrationTests`: HTTP-Verträge, Auth, Konfiguration und PostgreSQL-Persistenz.
- `CarGarage.AdminCli`: ersten Plattformadmin ohne öffentlichen Bootstrap-Endpunkt anlegen.

Application und Domain bleiben unabhängig von ASP.NET Core, EF Core und Npgsql.
Weitere Entities und Anwendungsfälle entstehen mit den nächsten Features.

## Validierung von Schritt 1

Release-Build und alle sechs HTTP-Integrationstests bestanden. Docker Compose
wurde mit API und PostgreSQL/PostGIS erfolgreich gestartet; beide Healthchecks,
Swagger und eine PostGIS-Abfrage wurden geprüft. Die dafür neu angelegten
Testcontainer und das leere Testvolume wurden anschließend entfernt.

## Validierung von Schritt 2

Release-Build ohne Warnungen und alle 20 Tests bestanden. Die DB-Tests prüfen
Migration und Wiederholbarkeit, Speicherung/Normalisierung, UUIDs und UTC-Zeitstempel,
Änderungen am Rollenarray, Eindeutigkeit pro Audience, Soft Delete,
Check-Constraints, Transaktions-Rollback und die DI-Anbindung der API.
Ungültige Datenbankkonfiguration wird beim Start abgewiesen.

Ein separates Compose-Testprojekt wurde mit einer leeren PostGIS-Datenbank
geprüft: TCP-Healthcheck, erfolgreicher Migrationscontainer, vorhandene User-Tabelle,
wiederholte Migration ohne Schemaänderung sowie gesunde API mit Health und Swagger.
Die dafür angelegten Container und das Testvolume wurden anschließend entfernt.

## Authentifizierung (Schritt 3)

Alle Endpunkte liegen unter `/api/auth/customer`, `/api/auth/workshop` oder
`/api/auth/admin`. Request-/Response-Felder folgen der Referenz. Auth-Antworten
enthalten `user` und `tokens` mit `accessToken`, `refreshToken` und `expiresIn`.
Passwort-Hashes und gespeicherte Refresh-Token-Hashes erscheinen nicht in Antworten.

| Aktion | Methode | Zugriff |
| --- | --- | --- |
| `/register` | POST | Customer öffentlich; Workshop/Admin nur mit ADMIN-Token und PLATFORM_ADMIN-Rolle |
| `/login` | POST | öffentlich; E-Mail und Passwort für das jeweilige Portal |
| `/refresh` | POST | öffentlich; gültiger Refresh-Token für das jeweilige Portal |
| `/logout` | POST | öffentlich; den übergebenen Refresh-Token löschen, wiederholbar |
| `/me` | GET | gültiger Bearer-Token für das jeweilige Portal |

Registrierung erwartet `email`, `password`, `firstName`, `lastName` und optional
`phone`. Rollen werden serverseitig gesetzt: CUSTOMER, WORKSHOP_MEMBER oder
PLATFORM_ADMIN. Der Client kann durch zusätzliche Felder keine Rollen wählen.
Werkstattkonten erhalten ihre Werkstattzuordnung erst in Schritt 4.

In Swagger zunächst registrieren oder einloggen, den `accessToken` kopieren und
unter **Authorize** eintragen. Der Dialog verwendet HTTP-Bearer-Auth, also nur
den Token ohne zusätzliches `Bearer` eingeben. Geschützte Operationen sind markiert.

JWTs nutzen HS256 und den Issuer `car-garage`; `sub`, `aud`, `roles`, `iat` und
`exp` werden ausgegeben. Der Standard für Access-Tokens beträgt 900 Sekunden,
für Refresh-Tokens 30 Tage; `.env` erlaubt die Anpassung. Der Referenzstandard
für Access-Tokens war 86400 Sekunden. Getrennte Auth-Schemes prüfen jeweils genau
ADMIN, WORKSHOP oder CUSTOMER. Bei geschützten Requests werden außerdem der
aktuelle Nutzerstatus und die aktuellen Rollen aus der DB geprüft. Sperren,
Soft Delete und Rollenentzug wirken damit auch auf bereits ausgegebene Tokens.

Refresh-Tokens bestehen aus 32 zufälligen Bytes und werden nur als SHA-256-Hash
persistiert. Rotation verbraucht den alten Token und speichert den neuen in einer
DB-Transaktion. Parallele Anfragen können einen Token höchstens einmal verwenden.
Ein Refresh im falschen Portal verbraucht den Token nicht. Abgelaufene Tokens
werden beim Refresh und zusätzlich stündlich durch einen Hintergrunddienst
entfernt. Logout entwertet den Refresh-Token; bereits ausgegebene Access-Tokens
bleiben bis zum Ablauf gültig, sofern Nutzerstatus und Rollen weiterhin passen.

Passwörter verwenden Standard-bcrypt mit WorkFactor 10. Für die Kompatibilität
mit bcrypt-Hashes werden höchstens **72 UTF-8-Bytes** akzeptiert, damit längere
Passwörter nicht stillschweigend abgeschnitten werden. Das weicht von der
128-Zeichen-Obergrenze der Referenz ab; mindestens acht Zeichen bleiben erforderlich.
Die Tests verwenden WorkFactor 4; die API verwendet standardmäßig 10.

## Ersten Admin anlegen

Nach erfolgreichem Compose-Start:

```bash
docker compose exec api dotnet /app/admin/CarGarage.AdminCli.dll admin@example.com
```

Das Passwort wird verdeckt abgefragt. Optional können Vor- und Nachname als
weitere Argumente angegeben werden. Es wird kein Passwort als Kommandozeilenargument
übergeben. Die CLI gibt nur E-Mail und ID des angelegten Admins aus, keine Tokens.
Eine doppelte E-Mail im Admin-Portal führt zu Exitcode 1.

Für lokale Entwicklung mit gesetztem `Database__ConnectionString`:

```bash
dotnet run --project tools/CarGarage.AdminCli -- admin@example.com Platform Admin
```

Anschließend unter `/api/auth/admin/login` anmelden. Bestehende `.env`-Dateien
müssen für Schritt 3 um `JWT_ACCESS_SECRET` ergänzt werden, bevor Compose startet.

## Validierung von Schritt 3

Build und lokaler Docker-Publish waren erfolgreich; alle 46 Integrationstests bestanden.
Geprüft sind die Auth-Verträge, drei Portale, Rollen-/Nutzerstatus, Signatur,
Issuer, Algorithmus und Ablauf von JWTs, doppelte/parallele Registrierung,
einmalige und parallele Refresh-Rotation, Logout, abgelaufene Tokens,
Token-Bereinigung, bcrypt-Grenzen und die Swagger-Security-Dokumentation.

Ein separates Compose-Testprojekt prüfte beide Migrationen, die Admin-CLI
inklusive Duplicate-Fehler, Login, Werkstatt-/Kundenregistrierung, Portaltrennung,
Refresh-Rotation und Logout gegen das lokal gebaute Docker-Image.
Die zugehörigen Testcontainer und Testdaten wurden anschließend entfernt.

## GitHub Actions

Repository: https://github.com/mofar1976/car-garage-dotnet
Der Inhalt dieses Ordners liegt direkt im Repository-Hauptverzeichnis;
`.github/workflows/ci.yml` gehört deshalb hierhin.

Der Workflow läuft bei Branch-Pushes, Pull Requests und manuellem Start:

1. .NET 10 einrichten, NuGet-Pakete und das lokale EF-Tool wiederherstellen.
2. Release-Build einschließlich Admin-CLI durchführen; Warnungen gelten als Fehler.
3. Prüfen, ob das EF-Modell vollständig durch Migrationen abgedeckt ist.
4. Alle Integrationstests ausführen; Testcontainers startet isoliertes PostgreSQL.
5. TRX-Testberichte als Workflow-Artefakt aufbewahren und das Docker-Image bauen.

Der Workflow ist auf CI beschränkt. Das Docker-Image wird für Linux/AMD64
gebaut und nicht in eine Registry veröffentlicht. Deployment ist vorerst nicht
vorgesehen. Zusätzliche Secrets sind nicht erforderlich; die Tests liefern ihre
eigene DB-/JWT-Konfiguration.

In GitHub müssen Actions und die verwendeten Actions zugelassen sein. Für `main`
kann eine Branch-Regel den Check **Build, test and Docker** vor einem Merge
verlangen. Die Actions sind auf Commit-IDs festgelegt; Dependabot schlägt
wöchentlich Aktualisierungen vor.

Die lokale Git-Verbindung wurde vorbereitet; Commit und Push erfolgen separat.
`PLAN.md`, `.env`, Build-Dateien und Testberichte werden nicht versioniert.
Der Workflow ist erst nach dem ersten Push auf GitHub aktiv.

Referenz: [GitHub: .NET CI](https://docs.github.com/en/actions/tutorials/build-and-test-code/net).

Die Workflow-Datei wurde lokal mit actionlint geprüft. Der Migrationsabgleich,
Release-Build mit Warnungen als Fehler und alle 46 Tests mit TRX-Bericht waren
lokal erfolgreich. Der erste GitHub-Lauf steht nach dem ersten Push noch aus.
