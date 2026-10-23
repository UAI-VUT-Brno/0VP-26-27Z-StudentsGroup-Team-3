# UAI.ZeroVP – REST API pro správu úkolů

**Jazyk:** [English](README.md) | **Čeština**

Ukázkové REST API pro správu úkolů (ToDo), vytvořené v .NET 10 jako výukový projekt. Řešení demonstruje vrstvenou architekturu, HTTP endpointy, automatizované testy, perzistentní JSON úložiště, řízení souběžnosti a provoz více instancí API na jednom počítači.

## Struktura repozitáře

```text
UAI.ZeroVP.sln                  Soubor solution
src/
├── UAI.ZeroVP.DTO/            Datové kontrakty API a validace
├── UAI.ZeroVP.Data/           Entity a implementace repozitářů
└── UAI.ZeroVP.API/            ASP.NET Core API, kontrolery a mapování
tests/                          Testy xUnit
docs/                           Technická dokumentace
scripts/                        Skripty pro lokální integrační testy
```

Řešení obsahuje tři hlavní projekty:

| Projekt | Odpovědnost | Závislosti |
| --- | --- | --- |
| `UAI.ZeroVP.DTO` | DTO požadavků a odpovědí, validace, výčet priorit | žádné |
| `UAI.ZeroVP.Data` | `TodoItem`, `ITodoRepository`, paměťové a JSON úložiště | DTO |
| `UAI.ZeroVP.API` | HTTP endpointy, mapování, dependency injection a middleware | DTO, Data |

Kontrolery závisejí na rozhraní `ITodoRepository`, nikoliv na konkrétní implementaci úložiště.

## Požadavky a lokální spuštění

- .NET 10 SDK
- PowerShell pro příklady ve Windows a volitelný integrační test dvou procesů
- Zapisovatelný adresář pro perzistentní JSON data

Sestavení a spuštění z kořenového adresáře repozitáře:

```powershell
dotnet build UAI.ZeroVP.sln --configuration Release
dotnet run --project src/UAI.ZeroVP.API
```

Projektový HTTP launch profil používá adresu **http://localhost:5258**. Jiné konfigurace spuštění mohou používat odlišné adresy. Kořenová cesta `/` přesměruje na Swagger UI, pokud je zapnuté, jinak na `/api/todos`.

## Úložiště dat

API obsahuje dvě implementace rozhraní `ITodoRepository`:

| Implementace | Kdy se používá | Perzistence | Sdílení mezi procesy |
| --- | --- | --- | --- |
| `JsonFileTodoRepository` | Běžná prostředí aplikace | JSON soubor | Ano, při stejné cestě k souboru |
| `InMemoryTodoRepository` | Prostředí `LoadTest` | Pouze operační paměť | Ne |

### Perzistentní JSON úložiště

Běžně spuštěná aplikace standardně používá `JsonFileTodoRepository`. Sekce `TodoStorage` určuje cestu k souboru a vkládání ukázkových úkolů. Aktuální konfigurace používá `data/todos.json` a při inicializaci nového úložiště povoluje ukázková data.

Konfiguraci lze přepsat proměnnými prostředí, například v PowerShellu:

```powershell
$env:TodoStorage__FilePath = "C:\Temp\ZeroVP\todos.json"
$env:TodoStorage__SeedSampleData = "false"
```

Všechny procesy API, které sdílejí úložiště, musí používat **stejnou absolutní cestu k souboru**. Relativní cesta závisí na pracovním adresáři procesu a pro multi-instance provoz není vhodná.

JSON repozitář používá meziprocesový zamykací soubor, před operacemi načítá aktuální obsah a změny zapisuje přes dočasný soubor s následným nahrazením. Poškozený JSON zjištěný při startu zazálohuje; poškození během běžného provozu nepřepíše potichu novými daty.

Pro zachování dat po nahrazení Docker kontejneru je potřeba správně nastavený svazek (volume) nebo bind mount. Více kontejnerů musí mít vhodný přístup ke stejnému úložišti a souborovému zamykání.

### Paměťové úložiště

Prostředí `LoadTest` používá `InMemoryTodoRepository` pro výkonnostní HTTP testy. Data zanikají s procesem. **Pro demonstraci sdíleného JSON úložiště nepoužívej prostředí `LoadTest`.**

## API endpointy

| Metoda | Cesta | Popis |
| --- | --- | --- |
| `GET` | `/api/todos` | Seznam úkolů; volitelný filtr `?isCompleted=true` nebo `false` |
| `GET` | `/api/todos/{id}` | Detail úkolu |
| `POST` | `/api/todos` | Vytvoření úkolu (`201 Created`, hlavička `Location`) |
| `POST` | `/api/todos/repeat` | Vytvoření týdenní řady samostatných úkolů |
| `PUT` | `/api/todos/{id}` | Úplná aktualizace včetně očekávané verze |
| `POST` | `/api/todos/{id}/complete` | Označení úkolu za dokončený |
| `DELETE` | `/api/todos/{id}` | Smazání úkolu (`204 No Content`) |
| `GET` | `/api/info` | Informace o instanci včetně unikátního `instanceId` |
| `GET` | `/health` | Kontrola zdraví API |
| `GET` | `/swagger` | Interaktivní Swagger UI, pokud je povoleno |
| `GET` | `/openapi/v1.json` | Strojově čitelný popis OpenAPI, pokud je povoleno |

Chybové odpovědi používají standardní ASP.NET Core `ProblemDetails`.

`POST /api/todos/repeat` přijímá název, volitelný popis, prioritu, UTC termín `firstDueAtUtc` a `repeatCount` (1–52 dalších týdenních opakování). Každý výskyt má své ID.

### Optimistic concurrency

Každý úkol obsahuje hodnotu `version`, která začíná na `1`. Úspěšné aktualizace ji zvyšují. Požadavek `PUT` musí obsahovat verzi, kterou klient při čtení získal:

```json
{
  "title": "Updated task",
  "description": "Example update",
  "priority": "Normal",
  "isCompleted": false,
  "version": 1
}
```

Pokud mezitím úkol změnila jiná operace, požadavek se zastaralou verzí dostane **HTTP `409 Conflict`** a novější uložená data zůstanou zachována. Klient by měl aktuální úkol znovu načíst a před opakováním aktualizace konflikt vyřešit.

## Spuštění dvou instancí API

Nejdříve aplikaci sestav v režimu Release. Potom otevři **dva samostatné terminály PowerShellu** v kořenovém adresáři repozitáře. Oba musí odkazovat na stejný absolutní JSON soubor.

**Terminál A:**

```powershell
$env:TodoStorage__FilePath = Join-Path $env:TEMP "ZeroVP-MultiInstance\todos.json"
$env:TodoStorage__SeedSampleData = "false"
$env:DOTNET_ENVIRONMENT = "Production"
dotnet run --project src/UAI.ZeroVP.API --configuration Release --no-build --no-launch-profile -- --urls http://127.0.0.1:5258
```

**Terminál B:**

```powershell
$env:TodoStorage__FilePath = Join-Path $env:TEMP "ZeroVP-MultiInstance\todos.json"
$env:TodoStorage__SeedSampleData = "false"
$env:DOTNET_ENVIRONMENT = "Production"
dotnet run --project src/UAI.ZeroVP.API --configuration Release --no-build --no-launch-profile -- --urls http://127.0.0.1:5259
```

Ve třetím terminálu ověř odlišná ID instancí:

```powershell
Invoke-RestMethod http://127.0.0.1:5258/api/info
Invoke-RestMethod http://127.0.0.1:5259/api/info
```

Vytvoř úkol přes instanci A a ihned ho načti přes B:

```powershell
$task = Invoke-RestMethod -Uri http://127.0.0.1:5258/api/todos `
    -Method Post -ContentType "application/json" `
    -Body '{"title":"Shared storage test","priority":"Normal"}'
Invoke-RestMethod "http://127.0.0.1:5259/api/todos/$($task.id)"
```

Každou instanci ukonči `Ctrl+C`. Podrobnosti najdeš v [návodu pro více instancí](docs/multi-instance.md).

## Testování

Kompletní sadu .NET testů spusť z kořenového adresáře:

```powershell
dotnet test UAI.ZeroVP.sln --configuration Release
```

Poslední ověřený lokální běh: **84/84 úspěšných testů xUnit**. Relevantní testovací třídy:

- `JsonFileTodoRepositoryTests` — JSON persistence a inicializace.
- `JsonFileMultiInstanceTests` — dvě instance repozitáře nad jedním souborem, souběžné zápisy, konflikty verzí a integrita souboru.
- `TodoConcurrencyTests` — zvyšování verzí přes HTTP, zastaralé aktualizace a `409 Conflict`.
- `ApiLoadTests` a `HttpPerformanceTests` — HTTP zátěžové testování a měření doby odezvy.

Šest JSON multi-instance testů používá samostatné objekty repozitáře **v jednom procesu OS**. Pro ověření dvou skutečných procesů API a sdíleného úložiště spusť samostatný PowerShell integrační test:

```powershell
dotnet build UAI.ZeroVP.sln --configuration Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\test-multi-instance.ps1"
```

Skript spustí dvě API, ověří různá ID instancí, viditelnost sdílených dat, odmítnutí zastaralého zápisu, 40 souběžných zápisů a validitu JSON a poté procesy ukončí. Používá dočasná testovací data. Poslední provedení skončilo hlášením `ALL MULTI-PROCESS TESTS PASSED`.

PowerShell test **není součástí** `dotnet test` ani se automaticky nespouští v CI. Je potřeba ho spustit zvlášť; `ExecutionPolicy Bypass` se v uvedeném příkazu vztahuje jen na nově spuštěný proces PowerShellu. Spouštěj pouze skript z důvěryhodného zdroje.

## Swagger a OpenAPI

API vystavuje OpenAPI dokument pomocí .NET 10 a při povolení také Swagger UI. Swagger je standardně zapnutý v `Development` a lze ho explicitně povolit pomocí:

```powershell
$env:Swagger__Enabled = "true"
```

Projekt také obsahuje ukázkové HTTP požadavky v [`src/UAI.ZeroVP.API/UAI.ZeroVP.API.http`](src/UAI.ZeroVP.API/UAI.ZeroVP.API.http).

## Výukové materiály k Dockeru

API slouží jako základ cvičení k Dockerfile, kontejnerovým nástrojům a konfiguraci. Některá původní zadání předpokládají úložiště pouze v RAM; s aktuálním JSON úložištěm se scénáře restartu a replikace mohou chovat jinak, pokud se vhodně nenakonfigurují.

Docker image sestavuj z kořenového adresáře repozitáře, pokud používáš příslušné výukové Dockerfile. Adresáře `case-study-a-dockerfile`, `case-study-b-tooling` a `case-study-c-configuration` uváděné ve starších verzích README **nebyly součástí poskytnutého snímku projektu**; konkrétní příklady hledej ve výukových materiálech.

## Další dokumentace

- [Multi-instance JSON úložiště](docs/multi-instance.md)
- [Výkon API](docs/api-performance.md)
- [HTTP zátěžové testování](docs/api-load-testing.md)
- [Dokumentace OpenAPI](docs/OpenAPI.md)
- [Zabezpečení](docs/security.md)

Zdrojové soubory C# používají podle `.editorconfig` UTF-8 s BOM; Markdown soubory používají UTF-8 a konce řádků LF.
